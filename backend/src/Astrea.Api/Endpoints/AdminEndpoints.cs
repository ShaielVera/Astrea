using Astrea.Api.Authorization;
using Astrea.Application.Features.Agent;
using Astrea.Application.Features.Users.ObtenerResumenUsuarios;

namespace Astrea.Api.Endpoints;

internal static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin")
            .RequireAuthorization(Politicas.Administrador)
            .WithTags("Administración");

        admin.MapGet("/usuarios/resumen", async (ObtenerResumenUsuariosHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(ct)))
            .WithName("ObtenerResumenUsuarios")
            .Produces<ResumenUsuariosDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        admin.MapGet("/asistente/metricas", async (DateOnly? desde, DateOnly? hasta, MetricasAsistente servicio, CancellationToken ct) =>
                (await servicio.ObtenerAsync(desde, hasta, ct)).AHttp())
            .WithName("MetricasDelAsistente")
            .WithDescription(
                "Uso del asistente en el período (días del instituto, ambos incluidos; por defecto los últimos 30): totales por resultado, " +
                "consultas por día, temas frecuentes (repetidos al menos 2 veces), temas sin respuesta, herramientas y motivos de falla. " +
                "Datos anónimos: no incluye quién preguntó.")
            .Produces<MetricasAsistenteDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return api;
    }
}
