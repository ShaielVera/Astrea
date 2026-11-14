namespace Astrea.Api.Authentication;

public static class FirebaseClaims
{
    /// <summary>En los ID Tokens de Firebase, "sub" es el UID del usuario.</summary>
    public const string Uid = "sub";

    public const string Email = "email";

    public const string EmailVerificado = "email_verified";

    /// <summary>Claim interno de Astrea con los roles leídos de la base de datos.</summary>
    public const string Rol = "astrea_rol";
}
