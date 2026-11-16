using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Agent;
using Astrea.Domain.Agent;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Infrastructure.Persistence.Repositories;

internal sealed class EfConversacionRepository(AppDbContext db) : IConversacionRepository
{
    public Task<Conversacion?> ObtenerAsync(Guid id, CancellationToken cancellationToken) =>
        db.Conversaciones.Include(conversacion => conversacion.Mensajes).SingleOrDefaultAsync(conversacion => conversacion.Id == id, cancellationToken);

    public void Agregar(Conversacion conversacion) => db.Conversaciones.Add(conversacion);

    public void Eliminar(Conversacion conversacion) => db.Conversaciones.Remove(conversacion);

    public async Task<PagedResult<ConversacionResumenDto>> ListarDeUsuarioAsync(Guid usuarioId, PageRequest pagina, CancellationToken cancellationToken)
    {
        var consulta = db.Conversaciones.AsNoTracking().Where(conversacion => conversacion.UsuarioId == usuarioId);
        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderByDescending(conversacion => conversacion.FechaUltimoMensaje)
            .ThenByDescending(conversacion => conversacion.Id)
            .Skip(pagina.Skip)
            .Take(pagina.PageSize)
            .Select(conversacion => new ConversacionResumenDto(conversacion.Id, conversacion.Titulo, conversacion.FechaCreacion, conversacion.FechaUltimoMensaje))
            .ToListAsync(cancellationToken);
        return PagedResult<ConversacionResumenDto>.From(items, pagina, total);
    }

    // Los mensajes se borran en cascada en la base.
    public Task<int> EliminarInactivasDesdeAsync(DateTimeOffset limite, CancellationToken cancellationToken) =>
        db.Conversaciones.Where(conversacion => conversacion.FechaUltimoMensaje <= limite).ExecuteDeleteAsync(cancellationToken);
}
