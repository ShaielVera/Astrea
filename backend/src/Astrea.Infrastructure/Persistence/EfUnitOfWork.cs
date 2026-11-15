using Astrea.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

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
    }
}
