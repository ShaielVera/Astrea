using Astrea.Api.Authorization;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Agent;
using Astrea.Application.Features.Users;
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

        admin.MapGet("/usuarios", async (string? texto, int? page, int? pageSize, GestionUsuarios servicio, CancellationToken ct) =>
                (await servicio.ListarAsync(texto, page, pageSize, ct)).AHttp())
            .WithName("ListarCuentas")
            .WithDescription("Todas las cuentas, las que iniciaron sesión más recientemente primero. texto: palabras del apellido, el nombre, el DNI o el email.")
            .Produces<PagedResult<CuentaDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        admin.MapPut("/usuarios/{usuarioId:guid}/roles/{rol}", async (Guid usuarioId, string rol, GestionUsuarios servicio, CancellationToken ct) =>
                (await servicio.DarRolAsync(usuarioId, rol, ct)).ASinContenido())
            .WithName("DarRol")
            .WithDescription("rol: ADMINISTRADOR o DOCENTE.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapDelete("/usuarios/{usuarioId:guid}/roles/{rol}", async (Guid usuarioId, string rol, GestionUsuarios servicio, CancellationToken ct) =>
                (await servicio.QuitarRolAsync(usuarioId, rol, ct)).ASinContenido())
            .WithName("QuitarRol")
            .WithDescription("Al quitar DOCENTE también se quitan sus materias. Nadie se quita a sí mismo el rol de administrador ni se deja al sistema sin administradores.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

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
