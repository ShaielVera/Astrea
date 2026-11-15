using Astrea.Application.Abstractions.Identity;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Results;
using Astrea.Domain.Users;

namespace Astrea.Application.Features.Auth;

/// <summary>
/// Usuario institucional de la sesión actual. La identidad sale siempre del token verificado,
/// nunca de datos que mande el cliente: así nadie puede actuar en nombre de otro.
/// </summary>
public sealed class UsuarioActual(ICurrentUser currentUser, IUserRepository usuarios)
{
    public async Task<Result<Usuario>> ObtenerAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUser.FirebaseUid))
        {
            return ErroresAuth.SinIdentidad;
        }

        var usuario = await usuarios.ObtenerPorFirebaseUidAsync(currentUser.FirebaseUid, cancellationToken);
        if (usuario is null)
        {
            return ErroresAuth.NoRegistrado;
        }

        if (!usuario.Activo)
        {
            return ErroresAuth.CuentaDesactivada;
        }

        return usuario;
    }
}
