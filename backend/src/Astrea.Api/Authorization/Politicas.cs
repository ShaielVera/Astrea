using Astrea.Api.Authentication;
using Astrea.Domain.Users;
using Microsoft.AspNetCore.Authorization;

namespace Astrea.Api.Authorization;

public static class Politicas
{
    public const string Estudiante = nameof(Estudiante);
    public const string Administrador = nameof(Administrador);
    public const string Docente = nameof(Docente);

    public static IServiceCollection AddPoliticasAstrea(this IServiceCollection services)
    {
        services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation, RolesDesdeBaseDeDatos>();

        services.AddAuthorizationBuilder()
            .AddPolicy(Estudiante, policy => policy.RequireAuthenticatedUser().RequireClaim(FirebaseClaims.Rol, Rol.CodigoEstudiante))
            .AddPolicy(Administrador, policy => policy.RequireAuthenticatedUser().RequireClaim(FirebaseClaims.Rol, Rol.CodigoAdministrador))
            .AddPolicy(Docente, policy => policy.RequireAuthenticatedUser().RequireClaim(FirebaseClaims.Rol, Rol.CodigoDocente))
            // Seguro por defecto: todo endpoint exige sesión salvo que se marque AllowAnonymous.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }
}
