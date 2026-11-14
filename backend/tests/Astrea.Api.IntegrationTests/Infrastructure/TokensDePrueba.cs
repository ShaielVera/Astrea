using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Astrea.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Emite tokens con la misma forma que los Firebase ID Tokens, firmados con una clave RSA local.
/// La API de test confía en esa clave en lugar de las claves públicas de Google.
/// </summary>
public static class TokensDePrueba
{
    public const string ProjectId = "astrea-test";

    public static readonly RsaSecurityKey ClaveConfiable = CrearClave("clave-confiable");

    public static readonly RsaSecurityKey ClaveAjena = CrearClave("clave-ajena");

    public static string Emisor(string projectId = ProjectId) => $"https://securetoken.google.com/{projectId}";

    public static string Crear(
        string uid,
        string email,
        bool emailVerificado = true,
        string projectId = ProjectId,
        DateTime? vence = null,
        SecurityKey? clave = null)
    {
        var expira = vence ?? DateTime.UtcNow.AddMinutes(30);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Emisor(projectId),
            Audience = projectId,
            IssuedAt = expira.AddHours(-1),
            NotBefore = expira.AddHours(-1),
            Expires = expira,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = uid,
                ["user_id"] = uid,
                ["email"] = email,
                ["email_verified"] = emailVerificado,
            },
            SigningCredentials = new SigningCredentials(clave ?? ClaveConfiable, SecurityAlgorithms.RsaSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static RsaSecurityKey CrearClave(string keyId) => new(RSA.Create(2048)) { KeyId = keyId };
}
