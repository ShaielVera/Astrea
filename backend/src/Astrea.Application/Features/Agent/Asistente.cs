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
using Astrea.Domain.Knowledge;
using Microsoft.Extensions.Logging;

namespace Astrea.Application.Features.Agent;

/// <summary>
/// El asistente de consultas (RAG): busca en los documentos oficiales los fragmentos que hablan de la pregunta,
/// le pide al modelo una respuesta limitada a esos fragmentos y solo la acepta si cita fuentes reales.
/// Si no hay evidencia suficiente, lo reconoce y deriva a Secretaría. Cada usuario ve solo sus conversaciones.
/// </summary>
public sealed class Asistente(
    UsuarioActual usuarioActual,
    IConversacionRepository conversaciones,
    IDocumentoRagRepository documentos,
    IEmbeddingService embeddings,
    IModeloLenguaje modelo,
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

        Conversacion conversacion;
        var nueva = comando.ConversacionId is null;
        if (nueva)
        {
            conversacion = Conversacion.Iniciar(Guid.CreateVersion7(), usuario.Valor.Id, DatosSensibles.Ocultar(texto), clock.UtcNow);
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

        // Nunca se guardan ni se envían al proveedor de IA datos personales que no hacen falta para responder.
        var pregunta = DatosSensibles.Ocultar(texto);

        RespuestaValidada respuesta;
        try
        {
            respuesta = await ResponderAsync(conversacion, pregunta, cancellationToken);
        }
        catch (Exception ex) when (ex is EmbeddingNoDisponibleException or ModeloNoDisponibleException)
        {
            logger.LogError(ex, "El asistente no pudo responder: {Motivo}", ex.Message);
            return ErroresAsistente.NoDisponible;
        }

        var (mensajePregunta, mensajeRespuesta) = conversacion.Registrar(pregunta, respuesta.Texto, respuesta.Fuentes, respuesta.SinEvidencia, clock.UtcNow);
        if (nueva)
        {
            conversaciones.Agregar(conversacion);
        }

        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return new IntercambioDto(conversacion.Id, conversacion.Titulo, MensajeDto.Desde(mensajePregunta), MensajeDto.Desde(mensajeRespuesta));
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

    private async Task<RespuestaValidada> ResponderAsync(Conversacion conversacion, string pregunta, CancellationToken cancellationToken)
    {
        // Una repregunta ("¿y en diciembre?") se entiende mejor buscando junto con la pregunta anterior.
        var busqueda = conversacion.UltimaPregunta is { } anterior ? $"{anterior}\n{pregunta}" : pregunta;
        var vector = (await embeddings.GenerarAsync([busqueda], PropositoEmbedding.Consulta, cancellationToken))[0];
        var encontrados = await documentos.BuscarSimilaresAsync(vector, Candidatos, cancellationToken);
        var evidencias = PoliticaRag.SeleccionarEvidencias(encontrados, configuracion.Asistente);

        // Sin evidencia no se consulta al modelo: no hay de dónde sacar una respuesta confirmada.
        if (evidencias.Count == 0)
        {
            return PoliticaRag.Abstencion();
        }

        var historial = conversacion.UltimosMensajes(MensajesDeContexto)
            .Select(mensaje => new MensajeModelo(mensaje.Rol == RolMensaje.Usuario, mensaje.Contenido))
            .ToList();
        var consulta = new ConsultaModelo(PoliticaRag.Instrucciones, historial, PoliticaRag.ArmarMensaje(pregunta, evidencias), PoliticaRag.ParaElModelo(evidencias));
        var respuesta = await modelo.ResponderAsync(consulta, cancellationToken);
        return PoliticaRag.Validar(respuesta, evidencias);
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
