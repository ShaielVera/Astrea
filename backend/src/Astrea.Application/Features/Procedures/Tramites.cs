using System.Globalization;
using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Documents;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Auth;
using Astrea.Domain.Procedures;
using Astrea.Domain.Users;

namespace Astrea.Application.Features.Procedures;

/// <summary>
/// Lo que hace el estudiante: ver el catálogo, pedir un trámite, seguir los suyos y descargar el PDF emitido.
/// Un trámite ajeno se responde como inexistente (404), para no revelar qué números existen.
/// </summary>
public sealed class SolicitudesTramite(
    UsuarioActual usuarioActual,
    ITramiteRepository tramites,
    DetalleTramites detalles,
    IFileStorage archivos,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<IReadOnlyList<TramiteTipoDto>> ListarTiposAsync(CancellationToken cancellationToken) =>
        (await tramites.ListarTiposAsync(incluirInactivos: false, cancellationToken)).Select(TramiteTipoDto.Desde).ToList();

    public async Task<Result<TramiteDetalleDto>> SolicitarAsync(SolicitarTramiteCommand comando, CancellationToken cancellationToken)
    {
        var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EsExitoso)
        {
            return usuario.Error!;
        }

        // Reintento de la misma solicitud (doble clic, red que se corta): se devuelve la que ya existe.
        if (comando.ClaveIdempotencia is { } clave
            && await tramites.ObtenerPorClaveIdempotenciaAsync(usuario.Valor.Id, clave, cancellationToken) is { } existente)
        {
            return await detalles.ArmarAsync(existente, usuario.Valor.Id, vistaAdministracion: false, cancellationToken);
        }

        var tipo = await tramites.ObtenerTipoAsync(comando.TipoId, cancellationToken);
        if (tipo is null)
        {
            return ErroresTramites.TipoNoEncontrado;
        }

        var tramite = Tramite.Solicitar(Guid.CreateVersion7(), usuario.Valor.Id, tipo, comando.Datos, comando.ClaveIdempotencia, clock.UtcNow);
        if (!await detalles.CarrerasValidasAsync(tipo, tramite.Datos, cancellationToken))
        {
            return ErroresTramites.CarreraInvalida;
        }

        // Si cargó sus carreras en "Mi perfil", solo puede pedir trámites de esas.
        if (usuario.Valor.CarrerasIds.Count > 0 && !DetalleTramites.CarrerasElegidas(tipo, tramite.Datos).All(usuario.Valor.CarrerasIds.Contains))
        {
            return ErroresTramites.CarreraAjena;
        }

        tramites.Agregar(tramite);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return await detalles.ArmarAsync(tramite, usuario.Valor.Id, vistaAdministracion: false, cancellationToken);
    }

    public async Task<Result<PagedResult<TramiteResumenDto>>> ListarMiosAsync(EstadoTramite[]? estados, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EsExitoso)
        {
            return usuario.Error!;
        }

        var filtro = new FiltroTramites(UsuarioId: usuario.Valor.Id, Estados: estados is { Length: > 0 } ? estados : null, MasRecientesPrimero: true);
        return await tramites.BuscarAsync(filtro, PageRequest.Create(page, pageSize), cancellationToken);
    }

    public async Task<Result<TramiteDetalleDto>> ObtenerMioAsync(Guid tramiteId, CancellationToken cancellationToken)
    {
        var tramite = await ObtenerPropioAsync(tramiteId, cancellationToken);
        if (!tramite.EsExitoso)
        {
            return tramite.Error!;
        }

        return await detalles.ArmarAsync(tramite.Valor, tramite.Valor.UsuarioId, vistaAdministracion: false, cancellationToken);
    }

    /// <summary>El titular cancela su trámite mientras no esté listo ni rechazado.</summary>
    public async Task<Result<TramiteDetalleDto>> CancelarAsync(Guid tramiteId, CancellationToken cancellationToken)
    {
        var tramite = await ObtenerPropioAsync(tramiteId, cancellationToken);
        if (!tramite.EsExitoso)
        {
            return tramite.Error!;
        }

        tramite.Valor.Cancelar(tramite.Valor.UsuarioId, clock.UtcNow);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return await detalles.ArmarAsync(tramite.Valor, tramite.Valor.UsuarioId, vistaAdministracion: false, cancellationToken);
    }

    /// <summary>Solo el titular descarga su documento; uno anulado ya no se entrega.</summary>
    public async Task<Result<ArchivoDto>> DescargarDocumentoAsync(Guid tramiteId, CancellationToken cancellationToken)
    {
        var tramite = await ObtenerPropioAsync(tramiteId, cancellationToken);
        if (!tramite.EsExitoso)
        {
            return tramite.Error!;
        }

        var documento = await tramites.ObtenerDocumentoDeTramiteAsync(tramiteId, cancellationToken);
        if (documento is null)
        {
            return ErroresTramites.DocumentoNoEncontrado;
        }

        if (documento.Estado == EstadoDocumento.Anulado)
        {
            return ErroresTramites.DocumentoAnulado;
        }

        return await Descargas.LeerAsync(archivos, documento, tramite.Valor, cancellationToken);
    }

    private async Task<Result<Tramite>> ObtenerPropioAsync(Guid tramiteId, CancellationToken cancellationToken)
    {
        var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EsExitoso)
        {
            return usuario.Error!;
        }

        var tramite = await tramites.ObtenerAsync(tramiteId, cancellationToken);
        if (tramite is null || !tramite.EsTitular(usuario.Valor.Id))
        {
            return ErroresTramites.TramiteNoEncontrado;
        }

        return tramite;
    }
}

/// <summary>
/// Bandeja de Secretaría (solo Administrador): revisar, tomar, aprobar (emitiendo el PDF), rechazar
/// y anular documentos. Cada transición registra al administrador que la hizo.
/// </summary>
public sealed class BandejaTramites(
    UsuarioActual usuarioActual,
    ITramiteRepository tramites,
    DetalleTramites detalles,
    EmisionDocumentos emision,
    IFileStorage archivos,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<PagedResult<TramiteResumenDto>>> BuscarAsync(
        EstadoTramite[]? estados,
        Guid? tipoId,
        string? texto,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        texto = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        if (texto?.Length > ErroresTramites.LargoMaximoTexto)
        {
            return ErroresTramites.TextoDemasiadoLargo;
        }

        var filtro = new FiltroTramites(Estados: estados is { Length: > 0 } ? estados : null, TipoId: tipoId, Texto: texto);
        return await tramites.BuscarAsync(filtro, PageRequest.Create(page, pageSize), cancellationToken);
    }

    public async Task<IReadOnlyList<TramiteTipoDto>> ListarTiposAsync(CancellationToken cancellationToken) =>
        (await tramites.ListarTiposAsync(incluirInactivos: true, cancellationToken)).Select(TramiteTipoDto.Desde).ToList();

    public async Task<Result<TramiteDetalleDto>> ObtenerAsync(Guid tramiteId, CancellationToken cancellationToken)
    {
        var tramite = await tramites.ObtenerAsync(tramiteId, cancellationToken);
        if (tramite is null)
        {
            return ErroresTramites.TramiteNoEncontrado;
        }

        return await detalles.ArmarAsync(tramite, usuarioActualId: null, vistaAdministracion: true, cancellationToken);
    }

    public Task<Result<TramiteDetalleDto>> TomarAsync(Guid tramiteId, CancellationToken cancellationToken) =>
        CambiarEstadoAsync(tramiteId, (tramite, adminId) => tramite.Tomar(adminId, clock.UtcNow), cancellationToken);

    public Task<Result<TramiteDetalleDto>> RechazarAsync(Guid tramiteId, RechazarTramiteCommand comando, CancellationToken cancellationToken) =>
        CambiarEstadoAsync(tramiteId, (tramite, adminId) => tramite.Rechazar(adminId, comando.Motivo, clock.UtcNow), cancellationToken);

    /// <summary>
    /// Aprueba el trámite y, si su tipo tiene plantilla, emite el PDF oficial en la misma operación.
    /// Si el PDF no se puede generar o guardar, el trámite no pasa a "Listo" y se puede reintentar.
    /// Aprobar de nuevo un trámite listo no emite otro documento (idempotente).
    /// </summary>
    public async Task<Result<TramiteDetalleDto>> AprobarAsync(Guid tramiteId, AprobarTramiteCommand comando, CancellationToken cancellationToken)
    {
        var administrador = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!administrador.EsExitoso)
        {
            return administrador.Error!;
        }

        var tramite = await tramites.ObtenerAsync(tramiteId, cancellationToken);
        if (tramite is null)
        {
            return ErroresTramites.TramiteNoEncontrado;
        }

        if (tramite.Estado == EstadoTramite.Listo)
        {
            return await detalles.ArmarAsync(tramite, usuarioActualId: null, vistaAdministracion: true, cancellationToken);
        }

        if (tramite.EstaFinalizado)
        {
            // Rechazado: el dominio explica por qué no se puede aprobar.
            tramite.Aprobar(administrador.Valor.Id, comando.Comentario, clock.UtcNow);
        }

        var emitido = await emision.EmitirSiCorrespondeAsync(tramite, cancellationToken);
        if (!emitido.EsExitoso)
        {
            return emitido.Error!;
        }

        tramite.Aprobar(administrador.Valor.Id, comando.Comentario, clock.UtcNow);
        try
        {
            await unitOfWork.GuardarCambiosAsync(cancellationToken);
        }
        catch when (emitido.Valor is { } rutaGuardada)
        {
            // No quedó registrado: se borra el archivo para no dejar PDFs huérfanos.
            await archivos.EliminarAsync(rutaGuardada, CancellationToken.None);
            throw;
        }

        return await detalles.ArmarAsync(tramite, usuarioActualId: null, vistaAdministracion: true, cancellationToken);
    }

    public async Task<Result<TramiteDetalleDto>> AnularDocumentoAsync(Guid tramiteId, AnularDocumentoCommand comando, CancellationToken cancellationToken)
    {
        var administrador = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!administrador.EsExitoso)
        {
            return administrador.Error!;
        }

        var tramite = await tramites.ObtenerAsync(tramiteId, cancellationToken);
        var documento = tramite is null ? null : await tramites.ObtenerDocumentoDeTramiteAsync(tramiteId, cancellationToken);
        if (tramite is null || documento is null)
        {
            return ErroresTramites.DocumentoNoEncontrado;
        }

        documento.Anular(administrador.Valor.Id, comando.Motivo, clock.UtcNow);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return await detalles.ArmarAsync(tramite, usuarioActualId: null, vistaAdministracion: true, cancellationToken);
    }

    public async Task<Result<ArchivoDto>> DescargarDocumentoAsync(Guid tramiteId, CancellationToken cancellationToken)
    {
        var tramite = await tramites.ObtenerAsync(tramiteId, cancellationToken);
        var documento = tramite is null ? null : await tramites.ObtenerDocumentoDeTramiteAsync(tramiteId, cancellationToken);
        if (tramite is null || documento is null)
        {
            return ErroresTramites.DocumentoNoEncontrado;
        }

        return await Descargas.LeerAsync(archivos, documento, tramite, cancellationToken);
    }

    private async Task<Result<TramiteDetalleDto>> CambiarEstadoAsync(Guid tramiteId, Action<Tramite, Guid> cambio, CancellationToken cancellationToken)
    {
        var administrador = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!administrador.EsExitoso)
        {
            return administrador.Error!;
        }

        var tramite = await tramites.ObtenerAsync(tramiteId, cancellationToken);
        if (tramite is null)
        {
            return ErroresTramites.TramiteNoEncontrado;
        }

        cambio(tramite, administrador.Valor.Id);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return await detalles.ArmarAsync(tramite, usuarioActualId: null, vistaAdministracion: true, cancellationToken);
    }
}

/// <summary>
/// Emisión del PDF oficial: datos oficiales del titular + plantilla vigente → PDF con QR → almacenamiento privado.
/// </summary>
public sealed class EmisionDocumentos(
    ITramiteRepository tramites,
    IUserRepository usuarios,
    DetalleTramites detalles,
    IPdfGenerator pdf,
    IFileStorage archivos,
    ConfiguracionDocumentos configuracion,
    IClock clock)
{
    /// <summary>
    /// Emite el documento si el tipo tiene plantilla y todavía no se emitió. Devuelve la ruta del archivo guardado
    /// (para poder borrarlo si después falla el guardado en la base), o null si no emitió nada.
    /// </summary>
    public async Task<Result<string?>> EmitirSiCorrespondeAsync(Tramite tramite, CancellationToken cancellationToken)
    {
        var plantilla = await tramites.ObtenerPlantillaVigenteAsync(tramite.TramiteTipoId, cancellationToken);
        if (plantilla is null || await tramites.ObtenerDocumentoDeTramiteAsync(tramite.Id, cancellationToken) is not null)
        {
            return Result<string?>.Exito(null);
        }

        // El PDF lleva nombre, apellido y DNI: los carga el estudiante en "Mi perfil" (o Secretaría desde la bandeja).
        var titular = await usuarios.ObtenerPorIdAsync(tramite.UsuarioId, cancellationToken);
        if (titular is not { TieneDatosOficiales: true })
        {
            return ErroresTramites.DatosOficialesIncompletos;
        }

        var tipo = (await tramites.ObtenerTipoAsync(tramite.TramiteTipoId, cancellationToken))!;
        var ahora = clock.UtcNow;
        var numero = NumeroTramite.Formatear(tramite.Numero);
        var valores = new Dictionary<string, string>(await detalles.ValoresParaMostrarAsync(tipo, tramite, cancellationToken))
        {
            ["nombreCompleto"] = $"{titular.Nombre} {titular.Apellido}",
            ["nombre"] = titular.Nombre!,
            ["apellido"] = titular.Apellido!,
            ["dni"] = FormatoDni.Formatear(titular.Dni!),
            ["legajo"] = titular.Legajo ?? string.Empty,
            ["numeroTramite"] = numero,
            ["fechaEmision"] = FechaEnLetras(ahora),
        };

        var codigo = await CodigoLibreAsync(cancellationToken);
        var contenido = pdf.Generar(new ContenidoDocumento(
            configuracion.Institucion,
            plantilla.Titulo,
            plantilla.Renderizar(valores),
            numero,
            codigo,
            configuracion.UrlPara(codigo),
            ahora,
            plantilla.Version));

        var id = Guid.CreateVersion7();
        var ruta = $"documentos/{ahora:yyyy}/{id}.pdf";
        await archivos.GuardarAsync(ruta, contenido, cancellationToken);

        tramites.Agregar(DocumentoEmitido.Emitir(id, tramite, plantilla, plantilla.Titulo, codigo, ruta, contenido, titular.Dni, ahora));
        return ruta;
    }

    private async Task<string> CodigoLibreAsync(CancellationToken cancellationToken)
    {
        string codigo;
        do
        {
            codigo = DocumentoEmitido.GenerarCodigo();
        }
        while (await tramites.ExisteCodigoValidacionAsync(codigo, cancellationToken));

        return codigo;
    }

    /// <summary>"15 de noviembre de 2026", en hora del instituto.</summary>
    private static string FechaEnLetras(DateTimeOffset ahora)
    {
        var local = TimeZoneInfo.ConvertTime(ahora, Domain.Exams.ZonaHorariaInstitucional.Zona);
        return local.ToString("d 'de' MMMM 'de' yyyy", CultureInfo.GetCultureInfo("es-AR"));
    }
}

/// <summary>Validación pública del QR: cualquiera puede comprobar un documento, viendo solo datos mínimos.</summary>
public sealed class ValidacionDocumentos(ITramiteRepository tramites)
{
    public async Task<Result<ValidacionDocumentoDto>> ValidarAsync(string codigo, CancellationToken cancellationToken)
    {
        var normalizado = DocumentoEmitido.NormalizarCodigo(codigo);
        var documento = normalizado is null ? null : await tramites.ObtenerDocumentoPorCodigoAsync(normalizado, cancellationToken);
        var tramite = documento is null ? null : await tramites.ObtenerAsync(documento.TramiteId, cancellationToken);
        if (documento is null || tramite is null)
        {
            return ErroresTramites.CodigoInvalido;
        }

        return new ValidacionDocumentoDto(
            documento.CodigoValidacion,
            documento.TipoDocumento,
            NumeroTramite.Formatear(tramite.Numero),
            documento.FechaEmision,
            documento.Estado,
            FormatoDni.Parcial(documento.DniUltimosDigitos),
            documento.HashSha256);
    }
}

/// <summary>Datos oficiales de un estudiante (los que van en los documentos). Solo Administrador.</summary>
public sealed class DatosOficiales(IUserRepository usuarios, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<Result<PersonaDto>> ActualizarAsync(Guid usuarioId, DatosOficialesCommand comando, CancellationToken cancellationToken)
    {
        var usuario = await usuarios.ObtenerPorIdAsync(usuarioId, cancellationToken);
        if (usuario is null)
        {
            return ErroresTramites.UsuarioNoEncontrado;
        }

        if (await usuarios.ExisteOtroConDniAsync(Usuario.NormalizarDni(comando.Dni), usuario.Id, cancellationToken))
        {
            return ErroresTramites.DniEnUso;
        }

        usuario.ActualizarDatosOficiales(comando.Nombre, comando.Apellido, comando.Dni, comando.Legajo, clock.UtcNow);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return new PersonaDto(
            usuario.Id,
            usuario.Email,
            usuario.Nombre,
            usuario.Apellido,
            usuario.Legajo,
            usuario.Dni,
            usuario.EmailContacto,
            usuario.EmailContactoVerificadoEn,
            usuario.Celular,
            usuario.CelularVerificadoEn);
    }
}

/// <summary>Plantillas de los documentos: consultar la vigente de cada tipo y crear una versión nueva. Solo Administrador.</summary>
public sealed class PlantillasDocumento(ITramiteRepository tramites, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<IReadOnlyList<PlantillaDeTipoDto>> ListarAsync(CancellationToken cancellationToken)
    {
        var resultado = new List<PlantillaDeTipoDto>();
        foreach (var tipo in await tramites.ListarTiposAsync(incluirInactivos: true, cancellationToken))
        {
            var vigente = await tramites.ObtenerPlantillaVigenteAsync(tipo.Id, cancellationToken);
            resultado.Add(new PlantillaDeTipoDto(tipo.Id, tipo.Nombre, vigente is null ? null : ADto(vigente), PlantillaDocumento.MarcadoresPermitidos(tipo)));
        }

        return resultado;
    }

    /// <summary>Las versiones anteriores no se modifican: los documentos ya emitidos siguen apuntando a la suya.</summary>
    public async Task<Result<PlantillaDto>> CrearVersionAsync(Guid tipoId, NuevaVersionPlantillaCommand comando, CancellationToken cancellationToken)
    {
        var tipo = await tramites.ObtenerTipoAsync(tipoId, cancellationToken);
        if (tipo is null)
        {
            return ErroresTramites.TipoNoEncontrado;
        }

        var vigente = await tramites.ObtenerPlantillaVigenteAsync(tipoId, cancellationToken);
        var nueva = PlantillaDocumento.Crear(Guid.CreateVersion7(), tipo, (vigente?.Version ?? 0) + 1, comando.Titulo, comando.Cuerpo, clock.UtcNow);
        tramites.Agregar(nueva);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return ADto(nueva);
    }

    private static PlantillaDto ADto(PlantillaDocumento plantilla) =>
        new(plantilla.Id, plantilla.Version, plantilla.Titulo, plantilla.Cuerpo, plantilla.CreadaAt);
}

/// <summary>Lectura del PDF guardado, compartida por la descarga del titular y la de administración.</summary>
internal static class Descargas
{
    public static async Task<Result<ArchivoDto>> LeerAsync(IFileStorage archivos, DocumentoEmitido documento, Tramite tramite, CancellationToken cancellationToken)
    {
        var contenido = await archivos.LeerAsync(documento.StoragePath, cancellationToken);
        if (contenido is null)
        {
            return ErroresTramites.DocumentoNoEncontrado;
        }

        var nombre = $"{NumeroTramite.Formatear(tramite.Numero)}-{documento.TipoDocumento.ToLowerInvariant().Replace(' ', '-')}.pdf";
        return new ArchivoDto(nombre, "application/pdf", contenido);
    }
}

/// <summary>Arma el detalle de un trámite para el estudiante o para la administración, y valida las carreras elegidas.</summary>
public sealed class DetalleTramites(ITramiteRepository tramites, IAcademicRepository academico, ConfiguracionDocumentos configuracion)
{
    /// <summary>Los campos de tipo carrera deben apuntar a una carrera existente y activa.</summary>
    public async Task<bool> CarrerasValidasAsync(TramiteTipo tipo, IReadOnlyDictionary<string, string> datos, CancellationToken cancellationToken)
    {
        foreach (var campo in tipo.Campos.Where(campo => campo.Tipo == TipoCampo.Carrera))
        {
            if (datos.TryGetValue(campo.Clave, out var valor)
                && await academico.ObtenerCarreraAsync(Guid.Parse(valor), cancellationToken) is not { Activa: true })
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Las carreras que eligió en los campos de tipo carrera del formulario.</summary>
    public static IEnumerable<Guid> CarrerasElegidas(TramiteTipo tipo, IReadOnlyDictionary<string, string> datos) =>
        tipo.Campos
            .Where(campo => campo.Tipo == TipoCampo.Carrera && datos.ContainsKey(campo.Clave))
            .Select(campo => Guid.Parse(datos[campo.Clave]));

    /// <summary>Datos del formulario listos para mostrar o imprimir: la carrera por su nombre, las fechas como dd/MM/aaaa.</summary>
    public async Task<IReadOnlyDictionary<string, string>> ValoresParaMostrarAsync(TramiteTipo tipo, Tramite tramite, CancellationToken cancellationToken)
    {
        var valores = new Dictionary<string, string>();
        foreach (var campo in tipo.Campos)
        {
            if (tramite.Datos.TryGetValue(campo.Clave, out var valor))
            {
                valores[campo.Clave] = await ValorParaMostrarAsync(campo, valor, cancellationToken);
            }
        }

        return valores;
    }

    public async Task<TramiteDetalleDto> ArmarAsync(Tramite tramite, Guid? usuarioActualId, bool vistaAdministracion, CancellationToken cancellationToken)
    {
        var tipo = (await tramites.ObtenerTipoAsync(tramite.TramiteTipoId, cancellationToken))!;
        var involucrados = tramite.Eventos.Select(evento => evento.ActorUsuarioId)
            .Append(tramite.UsuarioId)
            .Concat(tramite.AdministradorAsignadoId is { } asignado ? [asignado] : []);
        var personas = await tramites.ObtenerPersonasAsync(involucrados.Distinct(), cancellationToken);

        var valores = await ValoresParaMostrarAsync(tipo, tramite, cancellationToken);
        var datos = tipo.Campos
            .Where(campo => valores.ContainsKey(campo.Clave))
            .Select(campo => new DatoTramiteDto(campo.Clave, campo.Etiqueta, valores[campo.Clave]))
            .ToList();

        // El estudiante ve "Vos" o "Secretaría"; la administración ve quién hizo cada cambio.
        string Actor(Guid actorId) => vistaAdministracion
            ? personas.GetValueOrDefault(actorId)?.NombreParaMostrar ?? "Usuario eliminado"
            : actorId == usuarioActualId ? "Vos" : "Secretaría";

        var historial = tramite.Eventos
            .OrderBy(evento => evento.Fecha)
            .ThenBy(evento => evento.Id)
            .Select(evento => new EventoTramiteDto(evento.EstadoAnterior, evento.EstadoNuevo, evento.Comentario, Actor(evento.ActorUsuarioId), evento.Fecha))
            .ToList();

        var documento = await tramites.ObtenerDocumentoDeTramiteAsync(tramite.Id, cancellationToken);
        var emiteDocumento = await tramites.ObtenerPlantillaVigenteAsync(tipo.Id, cancellationToken) is not null;

        return new TramiteDetalleDto(
            tramite.Id,
            NumeroTramite.Formatear(tramite.Numero),
            tipo.Id,
            tipo.Nombre,
            tramite.Estado,
            tramite.FechaSolicitud,
            tramite.UpdatedAt,
            tramite.ObservacionAdministrativa,
            personas[tramite.UsuarioId],
            vistaAdministracion && tramite.AdministradorAsignadoId is { } adminId ? personas.GetValueOrDefault(adminId)?.NombreParaMostrar : null,
            datos,
            historial,
            emiteDocumento,
            documento is null ? null : ADto(documento));
    }

    private DocumentoDto ADto(DocumentoEmitido documento) =>
        new(
            documento.Id,
            documento.TipoDocumento,
            documento.CodigoValidacion,
            configuracion.UrlPara(documento.CodigoValidacion),
            documento.FechaEmision,
            documento.Estado,
            documento.VersionPlantilla,
            documento.HashSha256,
            documento.MotivoAnulacion);

    private async Task<string> ValorParaMostrarAsync(CampoTramite campo, string valor, CancellationToken cancellationToken) =>
        campo.Tipo switch
        {
            TipoCampo.Carrera => (await academico.ObtenerCarreraAsync(Guid.Parse(valor), cancellationToken))?.Nombre ?? "Carrera eliminada",
            TipoCampo.Fecha => DateOnly.ParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            _ => valor,
        };
}
