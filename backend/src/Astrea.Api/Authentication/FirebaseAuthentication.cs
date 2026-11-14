using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Astrea.Api.Authentication;

public sealed class FirebaseOptions
{
    public const string Seccion = "Firebase";

    /// <summary>ID del proyecto de Firebase. Es el "audience" de los ID Tokens.</summary>
    public string ProjectId { get; init; } = string.Empty;

    /// <summary>
    /// Acepta los tokens sin firma del Firebase Auth Emulator. Solo se permite en Development y E2E.
    /// </summary>
    public bool UsarEmuladorAuth { get; init; }
}

public static class FirebaseAuthentication
{
    public const string AmbienteE2E = "E2E";

    /// <summary>
    /// Valida los Firebase ID Tokens como JWT: firma (claves públicas de Google), emisor,
    /// audiencia (Project ID) y vencimiento. No requiere la cuenta de servicio.
    /// </summary>
    public static IServiceCollection AddFirebaseAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var firebase = configuration.GetSection(FirebaseOptions.Seccion).Get<FirebaseOptions>() ?? new FirebaseOptions();

        if (string.IsNullOrWhiteSpace(firebase.ProjectId))
        {
            throw new InvalidOperationException("Falta configurar 'Firebase:ProjectId'.");
        }

        if (firebase.UsarEmuladorAuth && !(environment.IsDevelopment() || environment.IsEnvironment(AmbienteE2E)))
        {
            throw new InvalidOperationException(
                "'Firebase:UsarEmuladorAuth' solo se permite en los ambientes Development y E2E.");
        }

        var emisor = $"https://securetoken.google.com/{firebase.ProjectId}";

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = emisor;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = emisor,
                    ValidateAudience = true,
                    ValidAudience = firebase.ProjectId,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = FirebaseClaims.Uid,
                    RoleClaimType = FirebaseClaims.Rol,
                };

                if (firebase.UsarEmuladorAuth)
                {
                    // El emulador emite tokens con alg "none": no hay firma que validar ni claves que descargar.
                    options.Configuration = new OpenIdConnectConfiguration();
                    options.TokenValidationParameters.RequireSignedTokens = false;
                    options.TokenValidationParameters.SignatureValidator = (token, _) => new JsonWebToken(token);
                }
            });

        return services;
    }
}
