using Astrea.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Astrea.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictoDeConcurrenciaException(ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new RegistroDuplicadoException(ex);
        }
    }

    public void DescartarCambios() => db.ChangeTracker.Clear();
}
