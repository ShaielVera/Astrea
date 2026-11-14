using Astrea.Domain.Users;

namespace Astrea.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<Usuario?> ObtenerPorFirebaseUidAsync(string firebaseUid, CancellationToken cancellationToken);

    void Agregar(Usuario usuario);

    /// <summary>Cantidad de usuarios activos por código de rol.</summary>
    Task<IReadOnlyDictionary<string, int>> ContarActivosPorRolAsync(CancellationToken cancellationToken);
}
