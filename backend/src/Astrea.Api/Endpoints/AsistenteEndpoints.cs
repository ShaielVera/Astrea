using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Agent;

namespace Astrea.Api.Endpoints;

internal static class AsistenteEndpoints
{
    public static RouteGroupBuilder MapAsistenteEndpoints(this RouteGroupBuilder api)
    {
        // Cualquier usuario con sesión. Cada uno ve solo sus conversaciones.
        var asistente = api.MapGroup("/asistente").WithTags("Asistente");

        asistente.MapPost("/preguntas", async (PreguntaCommand comando, Asistente servicio, CancellationToken ct) =>
                (await servicio.PreguntarAsync(comando, ct)).AHttp())
            .RequireRateLimiting(LimitesDeUso.Asistente)
            .WithName("PreguntarAlAsistente")
            .WithDescription(
                "Responde solo con información de los documentos oficiales indexados y devuelve las fuentes. Si no hay evidencia, " +
                "lo reconoce (sinEvidencia) y deriva a Secretaría. Sin conversacionId empieza una conversación nueva.")
            .Produces<IntercambioDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        asistente.MapGet("/conversaciones", async (int? page, int? pageSize, Asistente servicio, CancellationToken ct) =>
                (await servicio.ListarAsync(page, pageSize, ct)).AHttp())
            .WithName("MisConversaciones")
            .Produces<PagedResult<ConversacionResumenDto>>();

        asistente.MapGet("/conversaciones/{id:guid}", async (Guid id, Asistente servicio, CancellationToken ct) =>
                (await servicio.ObtenerAsync(id, ct)).AHttp())
            .WithName("ObtenerConversacion")
            .Produces<ConversacionDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        asistente.MapDelete("/conversaciones/{id:guid}", async (Guid id, Asistente servicio, CancellationToken ct) =>
                (await servicio.EliminarAsync(id, ct)).ASinContenido())
            .WithName("EliminarConversacion")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        asistente.MapPost("/conversaciones/{id:guid}/mensajes/{mensajeId:guid}/confirmar", async (Guid id, Guid mensajeId, Asistente servicio, CancellationToken ct) =>
                (await servicio.ConfirmarPropuestaAsync(id, mensajeId, ct)).AHttp())
            .WithName("ConfirmarPropuestaDeTramite")
            .WithDescription(
                "Crea el trámite que preparó el asistente, a nombre del usuario de la sesión. Es idempotente: confirmar dos veces crea uno solo. " +
                "El asistente nunca crea trámites por su cuenta.")
            .Produces<MensajeDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        asistente.MapPost("/conversaciones/{id:guid}/mensajes/{mensajeId:guid}/cancelar", async (Guid id, Guid mensajeId, Asistente servicio, CancellationToken ct) =>
                (await servicio.CancelarPropuestaAsync(id, mensajeId, ct)).AHttp())
            .WithName("CancelarPropuestaDeTramite")
            .Produces<MensajeDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        asistente.MapGet("/fuentes/{documentoId:guid}", async (Guid documentoId, Asistente servicio, CancellationToken ct) =>
                (await servicio.DescargarFuenteAsync(documentoId, ct)).AArchivo())
            .WithName("DescargarFuente")
            .WithDescription("El PDF de un documento oficial citado por el asistente (no los retirados).")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }
}
