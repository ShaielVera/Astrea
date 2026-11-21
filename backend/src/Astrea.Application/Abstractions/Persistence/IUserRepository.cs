using Astrea.Application.Contracts.Pagination;
using Astrea.Domain.Users;

namespace Astrea.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<Usuario?> ObtenerPorFirebaseUidAsync(string firebaseUid, CancellationToken cancellationToken);

    Task<Usuario?> ObtenerPorIdAsync(Guid usuarioId, CancellationToken cancellationToken);

    void Agregar(Usuario usuario);

    /// <summary>Si otra cuenta ya tiene ese DNI (para que dos cuentas no se adjudiquen la misma persona).</summary>
    Task<bool> ExisteOtroConDniAsync(string dni, Guid excluirUsuarioId, CancellationToken cancellationToken);

    /// <summary>El último código pedido por el usuario para ese canal (el único que se puede validar).</summary>
    Task<CodigoVerificacion?> UltimoCodigoAsync(Guid usuarioId, CanalContacto canal, CancellationToken cancellationToken);

    void Agregar(CodigoVerificacion codigo);

    /// <summary>Borra los códigos vencidos hace más de un día. Devuelve cuántos borró.</summary>
    Task<int> BorrarCodigosVencidosAsync(DateTimeOffset antesDe, CancellationToken cancellationToken);

    /// <summary>Cantidad de usuarios activos por código de rol.</summary>
    Task<IReadOnlyDictionary<string, int>> ContarActivosPorRolAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Las cuentas para Administración: las que iniciaron sesión más recientemente primero. Cada palabra de
    /// <paramref name="texto"/> tiene que aparecer en el apellido, el nombre, el DNI o el email (sin importar tildes).
    /// </summary>
    Task<PagedResult<Usuario>> ListarCuentasAsync(string? texto, PageRequest pagina, CancellationToken cancellationToken);
}
