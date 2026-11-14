namespace Astrea.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
