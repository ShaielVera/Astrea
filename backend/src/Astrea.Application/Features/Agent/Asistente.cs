using System.Text.Json;
using Astrea.Application.Abstractions.Ai;
using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Documents;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Auth;
using Astrea.Application.Features.Knowledge;
using Astrea.Application.Features.Procedures;
using Astrea.Domain.Agent;
using Astrea.Domain.Common;
using Astrea.Domain.Knowledge;
using Microsoft.Extensions.Logging;

namespace Astrea.Application.Features.Agent;

/// <summary>
/// El asistente de consultas. Combina documentos oficiales (RAG) con datos vigentes del sistema (herramientas):
/// el modelo propone qué herramienta usar, el backend valida y ejecuta, y la respuesta solo se acepta si cita fuentes reales.
/// Si no hay evidencia suficiente, lo reconoce y deriva a Secretaría. Un trámite solo se crea cuando el estudiante confirma
/// la propuesta. Cada usuario ve solo sus conversaciones.
/// </summary>
public sealed class Asistente(
    UsuarioActual usuarioActual,
    IConversacionRepository conversaciones,
    IDocumentoRagRepository documentos,
    IEmbeddingService embeddings,
    IModeloLenguaje modelo,
    HerramientasAsistente herramientas,
    SolicitudesTramite solicitudes,
    IFileStorage archivos,
    IUnitOfWork unitOfWork,
    IClock clock,
    ConfiguracionIa configuracion,
    ILogger<Asistente> logger)
{
    /// <summary>Cuántos fragmentos se piden a la búsqueda antes de filtrar por similitud.</summary>
    public const int Candidatos = 12;

    /// <summary>Mensajes anteriores que se le pasan al modelo para entender repreguntas.</summary>
    public const int MensajesDeContexto = 6;

    /// <summary>Idas y vueltas con el modelo por pregunta: acota costo, demora y bucles.</summary>
    public const int PasosMaximos = 4;

    /// <summary>Herramientas que se ejecutan por paso (el modelo puede pedir varias a la vez).</summary>
    public const int LlamadasPorPaso = 3;

    public async Task<Result<IntercambioDto>> PreguntarAsync(PreguntaCommand comando, CancellationToken cancellationToken)
    {
        var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EsExitoso)
        {
            return usuario.Error!;
        }

        var texto = comando.Mensaje?.Trim() ?? string.Empty;
        if (texto.Length == 0)
        {
            return ErroresAsistente.PreguntaVacia;
        }

        if (texto.Length > Conversacion.LargoMaximoPregunta)
        {
            return ErroresAsistente.PreguntaDemasiadoLarga;
        }

        // Nunca se guardan ni se envían al proveedor de IA datos personales que no hacen falta para responder.
        var pregunta = DatosSensibles.Ocultar(texto);

        Conversacion conversacion;
        var nueva = comando.ConversacionId is null;
        if (nueva)
        {
            conversacion = Conversacion.Iniciar(Guid.CreateVersion7(), usuario.Valor.Id, pregunta, clock.UtcNow);
        }
        else
        {
            var existente = await conversaciones.ObtenerAsync(comando.ConversacionId!.Value, cancellationToken);
            // Si es de otra persona responde igual que si no existiera: no revela qué ids existen.
            if (existente is null || !existente.PerteneceA(usuario.Valor.Id))
            {
                return ErroresAsistente.ConversacionNoEncontrada;
            }

            if (existente.CantidadDePreguntas >= Conversacion.PreguntasMaximas)
            {
                return ErroresAsistente.ConversacionLlena;
            }

            conversacion = existente;
        }

        RespuestaValidada respuesta;
        try
        {
            respuesta = await ResponderAsync(conversacion, pregunta, new ContextoHerramientas(usuario.Valor), cancellationToken);
        }
        catch (Exception ex) when (ex is EmbeddingNoDisponibleException or ModeloNoDisponibleException)
        {
            logger.LogError(ex, "El asistente no pudo responder: {Motivo}", ex.Message);
            return ErroresAsistente.NoDisponible;
        }

        var (mensajePregunta, mensajeRespuesta) = conversacion.Registrar(pregunta, respuesta.Texto, respuesta.Fuentes, respuesta.Tipo, clock.UtcNow, respuesta.Propuesta);
        if (nueva)
        {
            conversaciones.Agregar(conversacion);
        }

        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return new IntercambioDto(conversacion.Id, conversacion.Titulo, MensajeDto.Desde(mensajePregunta), MensajeDto.Desde(mensajeRespuesta));
    }

    /// <summary>
    /// El estudiante confirma la solicitud que preparó el asistente: recién ahí se crea el trámite, a su nombre.
    /// La clave de idempotencia es el id del mensaje, así que confirmar dos veces crea un solo trámite.
    /// </summary>
    public async Task<Result<MensajeDto>> ConfirmarPropuestaAsync(Guid conversacionId, Guid mensajeId, CancellationToken cancellationToken)
    {
        var conversacion = await PropiaAsync(conversacionId, cancellationToken);
        if (!conversacion.EsExitoso)
        {
            return conversacion.Error!;
        }

        if (conversacion.Valor.Mensajes.All(mensaje => mensaje.Id != mensajeId))
        {
            return ErroresAsistente.MensajeNoEncontrado;
        }

        // Una propuesta ya confirmada se informa sin volver a crear nada.
        var mensajeActual = conversacion.Valor.Mensajes.Single(mensaje => mensaje.Id == mensajeId);
        if (mensajeActual.Propuesta is { Estado: EstadoPropuesta.Confirmada })
        {
            return MensajeDto.Desde(mensajeActual);
        }

        PropuestaTramite propuesta;
        try
        {
            propuesta = conversacion.Valor.PropuestaPendiente(mensajeId, clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return Error.Conflicto("propuesta_no_disponible", ex.Message);
        }

        var tramite = await solicitudes.SolicitarAsync(new SolicitarTramiteCommand(propuesta.TipoId, propuesta.Datos, ClaveIdempotencia: mensajeId), cancellationToken);
        if (!tramite.EsExitoso)
        {
            return tramite.Error!;
        }

        var mensaje = conversacion.Valor.ConfirmarPropuesta(mensajeId, tramite.Valor.Id, tramite.Valor.Numero, clock.UtcNow);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return MensajeDto.Desde(mensaje);
    }

    public async Task<Result<MensajeDto>> CancelarPropuestaAsync(Guid conversacionId, Guid mensajeId, CancellationToken cancellationToken)
    {
        var conversacion = await PropiaAsync(conversacionId, cancellationToken);
        if (!conversacion.EsExitoso)
        {
            return conversacion.Error!;
        }

        if (conversacion.Valor.Mensajes.All(mensaje => mensaje.Id != mensajeId))
        {
            return ErroresAsistente.MensajeNoEncontrado;
        }

        try
        {
            var mensaje = conversacion.Valor.CancelarPropuesta(mensajeId, clock.UtcNow);
            await unitOfWork.GuardarCambiosAsync(cancellationToken);
            return MensajeDto.Desde(mensaje);
        }
        catch (DomainException ex)
        {
            return Error.Conflicto("propuesta_no_disponible", ex.Message);
        }
    }

    public async Task<Result<PagedResult<ConversacionResumenDto>>> ListarAsync(int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EsExitoso)
        {
            return usuario.Error!;
        }

        return await conversaciones.ListarDeUsuarioAsync(usuario.Valor.Id, PageRequest.Create(page, pageSize), cancellationToken);
    }

    public async Task<Result<ConversacionDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var conversacion = await PropiaAsync(id, cancellationToken);
        return conversacion.EsExitoso ? ConversacionDto.Desde(conversacion.Valor) : conversacion.Error!;
    }

    public async Task<Result<bool>> EliminarAsync(Guid id, CancellationToken cancellationToken)
    {
        var conversacion = await PropiaAsync(id, cancellationToken);
        if (!conversacion.EsExitoso)
        {
            return conversacion.Error!;
        }

        conversaciones.Eliminar(conversacion.Valor);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// El PDF de un documento citado. Cualquier usuario con sesión puede abrir los documentos oficiales que el asistente usa
    /// (vigentes o reemplazados por una versión nueva), pero no los retirados ni los que no llegaron a indexarse.
    /// </summary>
    public async Task<Result<ArchivoDto>> DescargarFuenteAsync(Guid documentoId, CancellationToken cancellationToken)
    {
        var documento = await documentos.ObtenerAsync(documentoId, cancellationToken);
        if (documento is not { Estado: EstadoIndexacion.Indexado or EstadoIndexacion.Reemplazado })
        {
            return ErroresAsistente.FuenteNoDisponible;
        }

        var contenido = await archivos.LeerAsync(documento.StoragePath, cancellationToken);
        return contenido is null
            ? ErroresAsistente.FuenteNoDisponible
            : new ArchivoDto(AdministracionDocumentosRag.NombreArchivo(documento), "application/pdf", contenido);
    }

    private async Task<RespuestaValidada> ResponderAsync(Conversacion conversacion, string pregunta, ContextoHerramientas contexto, CancellationToken cancellationToken)
    {
        // Una repregunta ("¿y en diciembre?") se entiende mejor buscando junto con la pregunta anterior.
        var busqueda = conversacion.UltimaPregunta is { } anterior ? $"{anterior}\n{pregunta}" : pregunta;
        var vector = (await embeddings.GenerarAsync([busqueda], PropositoEmbedding.Consulta, cancellationToken))[0];
        var encontrados = await documentos.BuscarSimilaresAsync(vector, Candidatos, cancellationToken);
        var evidencias = PoliticaRag.SeleccionarEvidencias(encontrados, configuracion.Asistente);

        var turnos = new List<TurnoAgente>();
        foreach (var mensaje in conversacion.UltimosMensajes(MensajesDeContexto))
        {
            turnos.Add(mensaje.Rol == RolMensaje.Usuario ? new TurnoUsuario(mensaje.Contenido) : new TurnoModelo(mensaje.Contenido));
        }

        turnos.Add(new TurnoUsuario(PoliticaRag.ArmarMensaje(pregunta, evidencias)));
        var definiciones = await herramientas.DefinicionesAsync(cancellationToken);

        for (var paso = 0; paso < PasosMaximos; paso++)
        {
            var llamadas = await modelo.SiguientePasoAsync(new ConsultaAgente(PoliticaRag.Instrucciones, definiciones, turnos.ToList()), cancellationToken);
            if (llamadas.Llamadas.Count == 0)
            {
                return PoliticaRag.Abstencion();
            }

            if (llamadas.Llamadas.FirstOrDefault(llamada => llamada.Nombre == HerramientasAsistente.Responder) is { } final)
            {
                return final.Argumentos.ValueKind == JsonValueKind.Object
                    ? PoliticaRag.Validar(LeerRespuesta(final.Argumentos), evidencias, contexto.Datos, contexto.Propuesta)
                    : PoliticaRag.Abstencion();
            }

            // Cada llamada recibe un resultado (algunos proveedores lo exigen); las que pasan del tope no se ejecutan.
            var resultados = new List<ResultadoHerramienta>(llamadas.Llamadas.Count);
            foreach (var (llamada, i) in llamadas.Llamadas.Select((llamada, i) => (llamada, i)))
            {
                logger.LogInformation("El asistente pide la herramienta {Herramienta}.", llamada.Nombre);
                var resultado = i < LlamadasPorPaso
                    ? await herramientas.EjecutarAsync(llamada, contexto, cancellationToken)
                    : new { error = $"Se ejecutan como máximo {LlamadasPorPaso} herramientas por paso." };
                resultados.Add(new ResultadoHerramienta(llamada.Nombre, resultado));
            }

            turnos.Add(llamadas);
            turnos.Add(new TurnoResultados(resultados));
        }

        logger.LogWarning("El asistente no terminó de responder en {Pasos} pasos.", PasosMaximos);
        return PoliticaRag.Abstencion();
    }

    private static RespuestaPropuesta LeerRespuesta(JsonElement argumentos)
    {
        string texto;
        try
        {
            texto = Argumentos.Texto(argumentos, "respuesta", Conversacion.LargoMaximoRespuesta * 2, obligatorio: false) ?? string.Empty;
        }
        catch (ArgumentoInvalidoException)
        {
            texto = string.Empty;
        }

        return new RespuestaPropuesta(
            texto,
            Argumentos.ListaDeTextos(argumentos, "fuentes"),
            Argumentos.Booleano(argumentos, "sinEvidencia"),
            Argumentos.Booleano(argumentos, "esAclaracion"));
    }

    private async Task<Result<Conversacion>> PropiaAsync(Guid id, CancellationToken cancellationToken)
    {
        var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EsExitoso)
        {
            return usuario.Error!;
        }

        var conversacion = await conversaciones.ObtenerAsync(id, cancellationToken);
        return conversacion is not null && conversacion.PerteneceA(usuario.Valor.Id) ? conversacion : ErroresAsistente.ConversacionNoEncontrada;
    }
}

/// <summary>Borra las conversaciones sin actividad hace más de <see cref="Conversacion.DiasDeConservacion"/> días.</summary>
public sealed class DepuracionConversaciones(IConversacionRepository conversaciones, IClock clock)
{
    public Task<int> DepurarAsync(CancellationToken cancellationToken) =>
        conversaciones.EliminarInactivasDesdeAsync(clock.UtcNow.AddDays(-Conversacion.DiasDeConservacion), cancellationToken);
}
