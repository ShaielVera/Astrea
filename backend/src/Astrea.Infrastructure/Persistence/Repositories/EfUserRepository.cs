using Astrea.Application.Abstractions.Persistence;
using Astrea.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Infrastructure.Persistence.Repositories;

internal sealed class EfUserRepository(AppDbContext db) : IUserRepository
{
    public Task<Usuario?> ObtenerPorFirebaseUidAsync(string firebaseUid, CancellationToken cancellationToken) =>
        db.Usuarios.SingleOrDefaultAsync(usuario => usuario.FirebaseUid == firebaseUid, cancellationToken);

    public void Agregar(Usuario usuario) => db.Usuarios.Add(usuario);

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
