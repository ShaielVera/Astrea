using Astrea.Api.Authorization;
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

        return api;
    }
}
