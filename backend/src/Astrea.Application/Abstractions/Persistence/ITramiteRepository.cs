using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Procedures;
using Astrea.Domain.Procedures;

namespace Astrea.Application.Abstractions.Persistence;

public interface ITramiteRepository
{
    Task<IReadOnlyList<TramiteTipo>> ListarTiposAsync(bool incluirInactivos, CancellationToken cancellationToken);

    Task<TramiteTipo?> ObtenerTipoAsync(Guid tipoId, CancellationToken cancellationToken);

    /// <summary>Trámite con su historial, listo para cambiar de estado.</summary>
    Task<Tramite?> ObtenerAsync(Guid tramiteId, CancellationToken cancellationToken);

    Task<Tramite?> ObtenerPorClaveIdempotenciaAsync(Guid usuarioId, Guid clave, CancellationToken cancellationToken);

    void Agregar(Tramite tramite);

    /// <summary>
    /// Trámites filtrados y paginados. Con <see cref="FiltroTramites.UsuarioId"/> se limita a los de ese titular.
    /// Orden estable: la bandeja ve primero los más antiguos; el estudiante, los más recientes.
    /// </summary>
    Task<PagedResult<TramiteResumenDto>> BuscarAsync(FiltroTramites filtro, PageRequest pagina, CancellationToken cancellationToken);

    /// <summary>Email, nombre y apellido de los usuarios indicados (titular y actores del historial).</summary>
    Task<IReadOnlyDictionary<Guid, PersonaDto>> ObtenerPersonasAsync(IEnumerable<Guid> usuarioIds, CancellationToken cancellationToken);
}
