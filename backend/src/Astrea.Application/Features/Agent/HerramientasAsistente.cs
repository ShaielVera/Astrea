using System.Globalization;
using System.Text;
using System.Text.Json;
using Astrea.Application.Abstractions.Ai;
using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Academic;
using Astrea.Application.Features.Exams;
using Astrea.Application.Features.LostAndFound;
using Astrea.Application.Features.Procedures;
using Astrea.Domain.Academic;
using Astrea.Domain.Agent;
using Astrea.Domain.Common;
using Astrea.Domain.Exams;
using Astrea.Domain.LostAndFound;
using Astrea.Domain.Procedures;
using Astrea.Domain.Users;

namespace Astrea.Application.Features.Agent;

/// <summary>Un dato vigente que consultó una herramienta, citable como [D1], [D2]…</summary>
public sealed record DatoConsultado(string Id, string Titulo, string Resumen, string Enlace);

/// <summary>Lo que se acumula mientras el asistente arma una respuesta: los datos consultados y la propuesta de trámite.</summary>
public sealed class ContextoHerramientas(Usuario usuario)
{
    private readonly List<DatoConsultado> _datos = [];

    /// <summary>Siempre el usuario de la sesión: el modelo no puede elegir sobre quién consulta.</summary>
    public Usuario Usuario { get; } = usuario;

    public IReadOnlyList<DatoConsultado> Datos => _datos;

    public PropuestaTramite? Propuesta { get; set; }

    public string Registrar(string titulo, string resumen, string enlace)
    {
        var id = $"D{_datos.Count + 1}";
        _datos.Add(new DatoConsultado(id, titulo, resumen, enlace));
        return id;
    }
}

/// <summary>Argumento inválido en una llamada del modelo: se le devuelve el motivo para que corrija o pregunte.</summary>
public sealed class ArgumentoInvalidoException(string mensaje) : Exception(mensaje);

/// <summary>
/// Las herramientas que puede pedir el modelo. Todas son de solo lectura salvo <see cref="ProponerTramite"/>, que tampoco
/// escribe nada: prepara una propuesta que el estudiante confirma con un botón. No existen herramientas para aprobar,
/// rechazar, entregar ni modificar planes. Cada llamada se valida (nombre, rol y argumentos) antes de ejecutarse.
/// </summary>
public sealed class HerramientasAsistente(
    IAcademicRepository academico,
    Correlatividades correlatividades,
    ConsultasMesas mesas,
    Hallazgos hallazgos,
    SolicitudesTramite solicitudes,
    ITramiteRepository tramites,
    IClock clock)
{
    public const string BuscarCorrelatividades = "buscar_correlatividades";
    public const string BuscarMesas = "buscar_mesas";
    public const string BuscarHallazgos = "buscar_hallazgos";
    public const string ConsultarMisTramites = "consultar_mis_tramites";
    public const string ProponerTramite = "proponer_tramite";

    /// <summary>Termina el turno con la respuesta para el estudiante (la ejecuta <see cref="Asistente"/>, no este servicio).</summary>
    public const string Responder = "responder";

    private const int LargoMaximoTexto = 100;
    private const int ResultadosMaximos = 8;

    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-AR");

    /// <summary>Herramientas que ejecuta este servicio y el rol que exige cada una.</summary>
    private static readonly IReadOnlyDictionary<string, string> RolRequerido = new Dictionary<string, string>
    {
        [BuscarCorrelatividades] = Rol.CodigoEstudiante,
        [BuscarMesas] = Rol.CodigoEstudiante,
        [BuscarHallazgos] = Rol.CodigoEstudiante,
        [ConsultarMisTramites] = Rol.CodigoEstudiante,
        [ProponerTramite] = Rol.CodigoEstudiante,
    };

    /// <summary>Las definiciones que se le muestran al modelo. Los tipos de trámite salen del catálogo vigente.</summary>
    public async Task<IReadOnlyList<DefinicionHerramienta>> DefinicionesAsync(CancellationToken cancellationToken)
    {
        var tipos = await solicitudes.ListarTiposAsync(cancellationToken);
        return
        [
            new(BuscarCorrelatividades, "Requisitos (correlatividades) para cursar y para rendir una materia, según el plan de estudios vigente.",
            [
                new("materia", TipoParametro.Texto, "Nombre de la materia, como lo escribió el estudiante.", true),
                new("carrera", TipoParametro.Texto, "Carrera, si el estudiante la mencionó.", false),
            ]),
            new(BuscarMesas, "Mesas de examen publicadas (fecha, hora, aula, tribunal e inscripción). Sin fechas, busca desde hoy.",
            [
                new("materia", TipoParametro.Texto, "Nombre de la materia o integrante del tribunal.", false),
                new("carrera", TipoParametro.Texto, "Carrera.", false),
                new("desde", TipoParametro.Fecha, "Desde (AAAA-MM-DD).", false),
                new("hasta", TipoParametro.Fecha, "Hasta (AAAA-MM-DD).", false),
            ]),
            new(BuscarHallazgos, "Objetos perdidos encontrados en el instituto que todavía no se retiraron (solo datos públicos).",
            [
                new("texto", TipoParametro.Texto, "Qué objeto busca (ej.: cartuchera, llaves).", false),
                new("categoria", TipoParametro.Texto, "Categoría del objeto.", false, Enum.GetNames<CategoriaObjeto>()),
            ]),
            new(ConsultarMisTramites, "Estado de los trámites del estudiante que está conversando (solo los suyos).",
            [
                new("numero", TipoParametro.Texto, "Número de trámite (ej.: T-000012), si lo mencionó.", false),
            ]),
            new(ProponerTramite,
                "Prepara una solicitud de trámite para que el estudiante la revise y la confirme con un botón. NO crea el trámite. " +
                "Tipos: " + string.Join("; ", tipos.Select(tipo => $"{tipo.Codigo} = {tipo.Nombre} (datos: {string.Join(", ", tipo.Campos.Where(c => c.Tipo != TipoCampo.Carrera).Select(c => $"{c.Clave}{(c.Obligatorio ? "*" : string.Empty)}"))})")),
            [
                new("tipo", TipoParametro.Texto, "Código del tipo de trámite.", true, tipos.Select(tipo => tipo.Codigo).ToList()),
                new("carrera", TipoParametro.Texto, "Carrera del estudiante.", true),
                new("datos", TipoParametro.ListaDeObjetos, "Los demás datos del formulario (los marcados con * son obligatorios). Fechas en AAAA-MM-DD.", false, Campos:
                [
                    new("clave", TipoParametro.Texto, "Nombre del dato.", true),
                    new("valor", TipoParametro.Texto, "Valor.", true),
                ]),
            ]),
            new(Responder,
                "Termina el turno con la respuesta para el estudiante. Usala siempre para responder, también para decir que no tenés información " +
                "o para preguntar un dato que falta.",
            [
                new("respuesta", TipoParametro.Texto, "Texto para el estudiante, con las citas [F1], [D1] de lo que usaste.", true),
                new("fuentes", TipoParametro.ListaDeTextos, "Ids de los fragmentos (F…) y datos (D…) que usaste.", true),
                new("sinEvidencia", TipoParametro.Booleano, "true si no tenés información confirmada para responder.", true),
                new("esAclaracion", TipoParametro.Booleano, "true si en lugar de responder le preguntás un dato que falta.", true),
            ]),
        ];
    }

    /// <summary>
    /// Ejecuta una llamada del modelo. Nunca lanza por culpa del modelo: un nombre desconocido, un rol insuficiente o un argumento
    /// inválido vuelven como { error } para que el modelo corrija o le avise al estudiante.
    /// </summary>
    public async Task<object> EjecutarAsync(LlamadaHerramienta llamada, ContextoHerramientas contexto, CancellationToken cancellationToken)
    {
        if (!RolRequerido.TryGetValue(llamada.Nombre, out var rol))
        {
            return new { error = $"La herramienta '{llamada.Nombre}' no existe o no está permitida. Solo podés usar las herramientas definidas." };
        }

        if (!contexto.Usuario.CodigosDeRol().Contains(rol))
        {
            return new { error = "El usuario no tiene permiso para usar esta herramienta." };
        }

        try
        {
            if (llamada.Argumentos.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentoInvalidoException("Los argumentos tienen que ser un objeto.");
            }

            return llamada.Nombre switch
            {
                BuscarCorrelatividades => await CorrelatividadesAsync(llamada.Argumentos, contexto, cancellationToken),
                BuscarMesas => await MesasAsync(llamada.Argumentos, contexto, cancellationToken),
                BuscarHallazgos => await HallazgosAsync(llamada.Argumentos, contexto, cancellationToken),
                ConsultarMisTramites => await MisTramitesAsync(llamada.Argumentos, contexto, cancellationToken),
                _ => await ProponerAsync(llamada.Argumentos, contexto, cancellationToken),
            };
        }
        catch (ArgumentoInvalidoException ex)
        {
            return new { error = ex.Message };
        }
    }

    private async Task<object> CorrelatividadesAsync(JsonElement argumentos, ContextoHerramientas contexto, CancellationToken cancellationToken)
    {
        var nombre = Argumentos.Texto(argumentos, "materia", LargoMaximoTexto, obligatorio: true)!;
        var carrera = await ResolverCarreraAsync(Argumentos.Texto(argumentos, "carrera", LargoMaximoTexto, obligatorio: false), cancellationToken);

        var encontradas = await academico.BuscarMateriasAsync(nombre, carrera?.Id, 10, cancellationToken);
        if (encontradas.Count == 0)
        {
            return new { error = $"No hay ninguna materia que se llame \"{nombre}\" en los planes vigentes." };
        }

        var exactas = encontradas.Where(materia => Normalizar(materia.Nombre) == Normalizar(nombre)).ToList();
        var candidatas = (exactas.Count > 0 ? exactas : encontradas).DistinctBy(materia => (materia.PlanId, materia.MateriaId)).ToList();
        if (candidatas.Count > 1)
        {
            return new
            {
                aclaracion = "Hay más de una materia que coincide: preguntale al estudiante cuál (o de qué carrera).",
                opciones = candidatas.Select(materia => $"{materia.Nombre} ({materia.Carrera}, {materia.Anio}° año)").ToList(),
            };
        }

        var elegida = candidatas[0];
        var resultado = await correlatividades.ObtenerAsync(elegida.PlanId, elegida.MateriaId, incluirInactivas: false, cancellationToken);
        if (!resultado.EsExitoso)
        {
            return new { error = resultado.Error!.Mensaje };
        }

        var materiaDto = resultado.Valor;
        var paraCursar = materiaDto.ParaCursar.Select(Requisito).ToList();
        var paraRendir = materiaDto.ParaRendir.Select(Requisito).ToList();
        var resumen = $"{materiaDto.Nombre} ({elegida.Carrera}). Para cursar: {Lista(paraCursar)}. Para rendir: {Lista(paraRendir)}.";
        var id = contexto.Registrar($"Correlatividades de {materiaDto.Nombre}", resumen, "/correlatividades");
        return new
        {
            id,
            materia = materiaDto.Nombre,
            carrera = elegida.Carrera,
            plan = elegida.Plan,
            anio = materiaDto.Anio,
            paraCursar,
            paraRendir,
            nota = "\"Regularizada\" y \"aprobada\" son condiciones distintas. Los requisitos marcados como alternativa: alcanza con cumplir uno del grupo.",
        };
    }

    private async Task<object> MesasAsync(JsonElement argumentos, ContextoHerramientas contexto, CancellationToken cancellationToken)
    {
        var materia = Argumentos.Texto(argumentos, "materia", LargoMaximoTexto, obligatorio: false);
        var carrera = await ResolverCarreraAsync(Argumentos.Texto(argumentos, "carrera", LargoMaximoTexto, obligatorio: false), cancellationToken);
        var desde = Argumentos.Fecha(argumentos, "desde");
        var hasta = Argumentos.Fecha(argumentos, "hasta");

        var resultado = await mesas.BuscarAsync(new FiltroMesas(desde, hasta, carrera?.Id, null, materia), 1, ResultadosMaximos, cancellationToken);
        if (!resultado.EsExitoso)
        {
            return new { error = resultado.Error!.Mensaje };
        }

        var encontradas = resultado.Valor;
        var lista = encontradas.Items.Select(mesa => new
        {
            materia = mesa.MateriaNombre,
            carrera = mesa.CarreraNombre,
            turno = mesa.Turno,
            llamado = mesa.Llamado,
            fecha = mesa.Fecha.ToString("dddd d/M/yyyy", Es),
            hora = mesa.Hora.ToString("HH:mm", Es),
            aula = mesa.Aula,
            tribunal = mesa.Tribunal,
            inscripcion = mesa.EstadoInscripcion switch
            {
                EstadoInscripcion.Abierta => $"abierta hasta el {mesa.InscripcionHasta:d/M}",
                EstadoInscripcion.Proxima => $"abre el {mesa.InscripcionDesde:d/M}",
                EstadoInscripcion.Cerrada => "cerrada",
                _ => "sin fechas publicadas",
            },
            estado = mesa.Estado == EstadoMesa.Cancelada ? $"CANCELADA: {mesa.MotivoCancelacion}" : "publicada",
            observaciones = mesa.Observaciones,
        }).ToList();

        var resumen = lista.Count == 0
            ? $"No hay mesas publicadas{(materia is null ? string.Empty : $" de \"{materia}\"")} en ese período."
            : string.Join("; ", lista.Take(3).Select(mesa => $"{mesa.materia}: {mesa.fecha} {mesa.hora} ({mesa.turno}, llamado {mesa.llamado}){(mesa.estado == "publicada" ? string.Empty : " — cancelada")}"))
              + (encontradas.TotalItems > 3 ? $" y {encontradas.TotalItems - 3} más" : string.Empty) + ".";
        var id = contexto.Registrar("Mesas de examen", resumen, "/mesas");
        return new { id, total = encontradas.TotalItems, mesas = lista };
    }

    private async Task<object> HallazgosAsync(JsonElement argumentos, ContextoHerramientas contexto, CancellationToken cancellationToken)
    {
        var texto = Argumentos.Texto(argumentos, "texto", LargoMaximoTexto, obligatorio: false);
        var categoria = Argumentos.Enum<CategoriaObjeto>(argumentos, "categoria");

        var resultado = await hallazgos.BuscarAsync(texto, categoria, null, 1, ResultadosMaximos, cancellationToken);
        if (!resultado.EsExitoso)
        {
            return new { error = resultado.Error!.Mensaje };
        }

        // Solo el DTO público: nunca la descripción privada, que es la que permite demostrar que el objeto es de uno.
        var lista = resultado.Valor.Items.Select(hallazgo => new
        {
            titulo = hallazgo.TituloPublico,
            categoria = hallazgo.Categoria.ToString(),
            dondeSeEncontro = hallazgo.SectorEncontrado,
            fecha = hallazgo.FechaHallazgo.ToString("d/M/yyyy", Es),
            dondeRetirarlo = hallazgo.PuntoResguardo.ToString(),
        }).ToList();

        var resumen = lista.Count == 0
            ? $"No hay objetos publicados que coincidan{(texto is null ? string.Empty : $" con \"{texto}\"")}."
            : string.Join("; ", lista.Take(3).Select(h => $"{h.titulo} (encontrado el {h.fecha}, se retira en {h.dondeRetirarlo})")) + ".";
        var id = contexto.Registrar("Hallazgos", resumen, "/hallazgos");
        return new
        {
            id,
            total = resultado.Valor.TotalItems,
            hallazgos = lista,
            aviso = "Para retirar un objeto hay que demostrar en el punto de resguardo que es tuyo (detalles privados, foto, factura o ticket).",
        };
    }

    private async Task<object> MisTramitesAsync(JsonElement argumentos, ContextoHerramientas contexto, CancellationToken cancellationToken)
    {
        var numero = Argumentos.Texto(argumentos, "numero", 20, obligatorio: false);

        // ListarMios usa el usuario de la sesión: el modelo no puede consultar trámites de otra persona.
        var resultado = await solicitudes.ListarMiosAsync(null, 1, numero is null ? 10 : PageRequest.MaxPageSize, cancellationToken);
        if (!resultado.EsExitoso)
        {
            return new { error = resultado.Error!.Mensaje };
        }

        var lista = resultado.Valor.Items
            .Where(tramite => numero is null || string.Equals(tramite.Numero, numero.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(tramite => new
            {
                numero = tramite.Numero,
                tipo = tramite.TipoNombre,
                estado = tramite.Estado switch
                {
                    EstadoTramite.EnProceso => "En proceso",
                    var estado => estado.ToString(),
                },
                solicitado = tramite.FechaSolicitud.ToString("d/M/yyyy", Es),
                ultimaNovedad = tramite.UpdatedAt.ToString("d/M/yyyy", Es),
            })
            .ToList();

        var resumen = lista.Count == 0
            ? numero is null ? "No tenés trámites pedidos." : $"No tenés un trámite con el número {numero}."
            : string.Join("; ", lista.Take(3).Select(t => $"{t.numero} {t.tipo}: {t.estado}")) + ".";
        var id = contexto.Registrar("Mis trámites", resumen, "/tramites");
        return new { id, tramites = lista };
    }

    private async Task<object> ProponerAsync(JsonElement argumentos, ContextoHerramientas contexto, CancellationToken cancellationToken)
    {
        var codigo = Argumentos.Texto(argumentos, "tipo", 40, obligatorio: true)!;
        var tipoDto = (await solicitudes.ListarTiposAsync(cancellationToken)).FirstOrDefault(tipo => string.Equals(tipo.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
        var tipo = tipoDto is null ? null : await tramites.ObtenerTipoAsync(tipoDto.Id, cancellationToken);
        if (tipo is null)
        {
            return new { error = $"No existe el tipo de trámite '{codigo}'." };
        }

        var nombreCarrera = Argumentos.Texto(argumentos, "carrera", LargoMaximoTexto, obligatorio: true)!;
        var carrera = await ResolverCarreraAsync(nombreCarrera, cancellationToken);
        var datos = Argumentos.Pares(argumentos, "datos")
            .ToDictionary(par => par.Clave, par => (string?)par.Valor, StringComparer.Ordinal);
        foreach (var campo in tipo.Campos.Where(campo => campo.Tipo == TipoCampo.Carrera))
        {
            datos[campo.Clave] = carrera!.Id.ToString();
        }

        // Se valida con las mismas reglas que una solicitud real, pero sin guardar nada.
        Tramite valido;
        try
        {
            valido = Tramite.Solicitar(Guid.NewGuid(), contexto.Usuario.Id, tipo, datos, null, clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return new { error = $"Faltan datos o hay datos inválidos: {ex.Message} Preguntáselos al estudiante." };
        }

        var resumen = tipo.Campos
            .Where(campo => valido.Datos.ContainsKey(campo.Clave))
            .Select(campo => new DatoPropuesta(campo.Etiqueta, campo.Tipo == TipoCampo.Carrera ? carrera!.Nombre : valido.Datos[campo.Clave]!))
            .ToList();
        contexto.Propuesta = new PropuestaTramite(tipo.Id, tipo.Nombre, valido.Datos.ToDictionary(par => par.Key, par => (string?)par.Value), resumen);

        return new
        {
            propuesta = new { tipo = tipo.Nombre, datos = resumen.Select(dato => $"{dato.Etiqueta}: {dato.Valor}") },
            aviso = "La solicitud TODAVÍA NO SE CREÓ. Decile al estudiante que la revise y la confirme con el botón \"Confirmar y enviar\".",
        };
    }

    /// <summary>La carrera del catálogo que corresponde al texto (por código o nombre, sin distinguir tildes). Null si no se indicó.</summary>
    private async Task<CarreraDto?> ResolverCarreraAsync(string? texto, CancellationToken cancellationToken)
    {
        if (texto is null)
        {
            return null;
        }

        var carreras = (await academico.ListarCarrerasAsync(PageRequest.Create(1, PageRequest.MaxPageSize), incluirInactivas: false, cancellationToken)).Items;
        var buscado = Normalizar(texto);
        var coincidencias = carreras
            .Where(carrera => Normalizar(carrera.Codigo) == buscado || Normalizar(carrera.Nombre) == buscado)
            .ToList();
        if (coincidencias.Count == 0)
        {
            coincidencias = carreras.Where(carrera => Normalizar(carrera.Nombre).Contains(buscado, StringComparison.Ordinal)).ToList();
        }

        return coincidencias.Count switch
        {
            1 => coincidencias[0],
            0 => throw new ArgumentoInvalidoException(
                $"No existe la carrera \"{texto}\". Carreras disponibles: {string.Join("; ", carreras.Select(carrera => carrera.Nombre))}."),
            _ => throw new ArgumentoInvalidoException(
                $"\"{texto}\" coincide con varias carreras: {string.Join("; ", coincidencias.Select(carrera => carrera.Nombre))}. Preguntale al estudiante cuál."),
        };
    }

    private static string Requisito(RequisitoDto requisito)
    {
        var condicion = requisito.Condicion == CondicionRequisito.Aprobada ? "aprobada" : "regularizada";
        var que = requisito.Nombre is { } nombre ? $"{nombre} {condicion}" : requisito.Descripcion ?? "condición a verificar en Secretaría";
        return requisito.GrupoAlternativa is { } grupo ? $"{que} (alternativa del grupo {grupo})" : que;
    }

    private static string Lista(IReadOnlyList<string> requisitos) => requisitos.Count == 0 ? "sin requisitos" : string.Join(", ", requisitos);

    internal static string Normalizar(string texto) =>
        new(texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
}

/// <summary>Lectura estricta de los argumentos que manda el modelo: tipos, largos y formatos.</summary>
internal static class Argumentos
{
    public static string? Texto(JsonElement argumentos, string nombre, int largoMaximo, bool obligatorio)
    {
        if (!argumentos.TryGetProperty(nombre, out var valor) || valor.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            || (valor.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(valor.GetString())))
        {
            return obligatorio ? throw new ArgumentoInvalidoException($"Falta el argumento '{nombre}'.") : null;
        }

        if (valor.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentoInvalidoException($"El argumento '{nombre}' tiene que ser texto.");
        }

        var texto = valor.GetString()!.Trim();
        return texto.Length <= largoMaximo ? texto : throw new ArgumentoInvalidoException($"El argumento '{nombre}' supera los {largoMaximo} caracteres.");
    }

    public static DateOnly? Fecha(JsonElement argumentos, string nombre)
    {
        var texto = Texto(argumentos, nombre, 10, obligatorio: false);
        if (texto is null)
        {
            return null;
        }

        return DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? fecha
            : throw new ArgumentoInvalidoException($"El argumento '{nombre}' tiene que ser una fecha AAAA-MM-DD.");
    }

    public static TEnum? Enum<TEnum>(JsonElement argumentos, string nombre)
        where TEnum : struct, System.Enum
    {
        var texto = Texto(argumentos, nombre, 40, obligatorio: false);
        if (texto is null)
        {
            return null;
        }

        return System.Enum.TryParse<TEnum>(texto, ignoreCase: true, out var valor) && System.Enum.IsDefined(valor) && !int.TryParse(texto, out _)
            ? valor
            : throw new ArgumentoInvalidoException($"'{texto}' no es un valor válido para '{nombre}'. Valores: {string.Join(", ", System.Enum.GetNames<TEnum>())}.");
    }

    public static bool Booleano(JsonElement argumentos, string nombre) =>
        argumentos.TryGetProperty(nombre, out var valor) && valor.ValueKind == JsonValueKind.True;

    public static IReadOnlyList<string> ListaDeTextos(JsonElement argumentos, string nombre)
    {
        if (!argumentos.TryGetProperty(nombre, out var valor) || valor.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return valor.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!.Trim()).Take(20).ToList();
    }

    public static IReadOnlyList<(string Clave, string Valor)> Pares(JsonElement argumentos, string nombre)
    {
        if (!argumentos.TryGetProperty(nombre, out var valor) || valor.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var pares = new List<(string, string)>();
        foreach (var item in valor.EnumerateArray().Take(20))
        {
            var clave = item.ValueKind == JsonValueKind.Object ? Texto(item, "clave", 40, obligatorio: true)! : throw new ArgumentoInvalidoException($"Cada elemento de '{nombre}' tiene que tener clave y valor.");
            var texto = Texto(item, "valor", TramiteTipo.LargoMaximoTextoLargo, obligatorio: false);
            if (texto is not null)
            {
                pares.Add((clave, texto));
            }
        }

        return pares;
    }
}
