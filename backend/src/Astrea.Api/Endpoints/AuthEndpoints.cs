using Astrea.Application.Features.Auth;
using Astrea.Application.Features.Auth.IniciarSesion;
using Astrea.Application.Features.Auth.ObtenerPerfil;

namespace Astrea.Api.Endpoints;

internal static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        // Se llama después de iniciar sesión en Firebase: da de alta o sincroniza al usuario.
        api.MapPost("/auth/sesion", async (IniciarSesionHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(ct)).AHttp())
            .RequireRateLimiting(LimitesDeUso.Sesion)
            .WithName("IniciarSesion")
            .WithTags("Auth")
            .Produces<PerfilDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        api.MapGet("/me", async (ObtenerPerfilHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(ct)).AHttp())
            .WithName("ObtenerPerfil")
            .WithTags("Auth")
            .Produces<PerfilDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }
}
