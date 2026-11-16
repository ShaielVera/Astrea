using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Agent;
using Astrea.Domain.Agent;

namespace Astrea.Application.Abstractions.Persistence;

public interface IConversacionRepository
{
    /// <summary>La conversación con todos sus mensajes, lista para agregar más.</summary>
    Task<Conversacion?> ObtenerAsync(Guid id, CancellationToken cancellationToken);

    void Agregar(Conversacion conversacion);

    void Eliminar(Conversacion conversacion);

    /// <summary>Las conversaciones del usuario, la más reciente primero.</summary>
    Task<PagedResult<ConversacionResumenDto>> ListarDeUsuarioAsync(Guid usuarioId, PageRequest pagina, CancellationToken cancellationToken);

    /// <summary>Borra (con sus mensajes) las conversaciones sin actividad desde <paramref name="limite"/>. Devuelve cuántas borró.</summary>
    Task<int> EliminarInactivasDesdeAsync(DateTimeOffset limite, CancellationToken cancellationToken);
}
