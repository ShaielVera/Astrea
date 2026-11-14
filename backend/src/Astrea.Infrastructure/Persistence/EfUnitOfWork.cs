using Astrea.Application.Abstractions.Persistence;

namespace Astrea.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
