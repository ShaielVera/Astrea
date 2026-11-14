using Astrea.Application.Abstractions.Identity;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Results;

namespace Astrea.Application.Features.Auth.ObtenerPerfil;

/// <summary>Devuelve el perfil del usuario autenticado. Solo lectura.</summary>
public sealed class ObtenerPerfilHandler(ICurrentUser currentUser, IUserRepository usuarios)
{
    public async Task<Result<PerfilDto>> HandleAsync(CancellationToken cancellationToken)
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

        return PerfilDto.Desde(usuario);
    }
}
