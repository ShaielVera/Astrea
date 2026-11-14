using Astrea.Application.Abstractions.Identity;

namespace Astrea.Api.Authentication;

/// <summary>Lee la identidad del token ya validado por el middleware de autenticación.</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public string? FirebaseUid =>
        Principal?.Identity?.IsAuthenticated == true ? Principal.FindFirst(FirebaseClaims.Uid)?.Value : null;

    public string? Email => Principal?.FindFirst(FirebaseClaims.Email)?.Value;

    public bool EmailVerificado =>
        bool.TryParse(Principal?.FindFirst(FirebaseClaims.EmailVerificado)?.Value, out var verificado) && verificado;
}
