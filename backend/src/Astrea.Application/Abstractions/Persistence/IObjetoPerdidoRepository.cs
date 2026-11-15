using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.LostAndFound;
using Astrea.Domain.LostAndFound;

namespace Astrea.Application.Abstractions.Persistence;

public interface IObjetoPerdidoRepository
{
    /// <summary>El hallazgo con su entrega (si la hubo), listo para modificar.</summary>
    Task<ObjetoPerdido?> ObtenerAsync(Guid id, CancellationToken cancellationToken);

    void Agregar(ObjetoPerdido objeto);

    /// <summary>Hallazgos filtrados y paginados: los encontrados más recientemente primero (orden estable por id).</summary>
    Task<PagedResult<ObjetoPerdido>> BuscarAsync(FiltroHallazgos filtro, PageRequest pagina, CancellationToken cancellationToken);

    /// <summary>Hallazgos cerrados (entregados o dados de baja) antes de <paramref name="limite"/>, con su entrega, hasta <paramref name="maximo"/>.</summary>
    Task<IReadOnlyList<ObjetoPerdido>> ListarCerradosAntesDeAsync(DateTimeOffset limite, int maximo, CancellationToken cancellationToken);

    /// <summary>Borra el hallazgo y su registro de entrega.</summary>
    void Eliminar(ObjetoPerdido objeto);

    Task<IReadOnlyDictionary<Guid, PersonaHallazgoDto>> ObtenerPersonasAsync(IEnumerable<Guid> usuarioIds, CancellationToken cancellationToken);
}
