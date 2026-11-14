namespace Astrea.Application.Abstractions.Identity;

/// <summary>
/// Puerto de entrada de la identidad autenticada. Los datos provienen del token de Firebase
/// ya verificado por el backend; nunca del cuerpo de la petición.
/// </summary>
public interface ICurrentUser
{
    string? FirebaseUid { get; }

    string? Email { get; }

    bool EmailVerificado { get; }
}
