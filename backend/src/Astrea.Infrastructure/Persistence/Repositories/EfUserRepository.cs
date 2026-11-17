using Astrea.Application.Abstractions.Persistence;
using Astrea.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Infrastructure.Persistence.Repositories;

internal sealed class EfUserRepository(AppDbContext db) : IUserRepository
{
    public Task<Usuario?> ObtenerPorFirebaseUidAsync(string firebaseUid, CancellationToken cancellationToken) =>
        db.Usuarios.SingleOrDefaultAsync(usuario => usuario.FirebaseUid == firebaseUid, cancellationToken);

    public Task<Usuario?> ObtenerPorIdAsync(Guid usuarioId, CancellationToken cancellationToken) =>
        db.Usuarios.SingleOrDefaultAsync(usuario => usuario.Id == usuarioId, cancellationToken);

    public void Agregar(Usuario usuario) => db.Usuarios.Add(usuario);

    public Task<bool> ExisteOtroConDniAsync(string dni, Guid excluirUsuarioId, CancellationToken cancellationToken) =>
        db.Usuarios.AnyAsync(usuario => usuario.Dni == dni && usuario.Id != excluirUsuarioId, cancellationToken);

    public Task<CodigoVerificacion?> UltimoCodigoAsync(Guid usuarioId, CanalContacto canal, CancellationToken cancellationToken) =>
        db.CodigosVerificacion
            .Where(codigo => codigo.UsuarioId == usuarioId && codigo.Canal == canal)
            .OrderByDescending(codigo => codigo.CreadoEn)
            .FirstOrDefaultAsync(cancellationToken);

    public void Agregar(CodigoVerificacion codigo) => db.CodigosVerificacion.Add(codigo);

    public Task<int> BorrarCodigosVencidosAsync(DateTimeOffset antesDe, CancellationToken cancellationToken) =>
        db.CodigosVerificacion.Where(codigo => codigo.VenceEn < antesDe).ExecuteDeleteAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<string, int>> ContarActivosPorRolAsync(CancellationToken cancellationToken)
    {
        var conteo = await db.UsuarioRoles
            .Where(asignacion => db.Usuarios.Any(usuario => usuario.Id == asignacion.UsuarioId && usuario.Activo))
            .Join(db.Roles, asignacion => asignacion.RolId, rol => rol.Id, (_, rol) => rol.Codigo)
            .GroupBy(codigo => codigo)
            .Select(grupo => new { Codigo = grupo.Key, Cantidad = grupo.Count() })
            .ToListAsync(cancellationToken);

        return conteo.ToDictionary(item => item.Codigo, item => item.Cantidad);
    }
}
