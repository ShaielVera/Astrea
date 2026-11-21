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
    /// Orden estable: la bandeja y el estudiante ven primero los más recientes.
    /// </summary>
    Task<PagedResult<TramiteResumenDto>> BuscarAsync(FiltroTramites filtro, PageRequest pagina, CancellationToken cancellationToken);

    /// <summary>Email, nombre y apellido de los usuarios indicados (titular y actores del historial).</summary>
    Task<IReadOnlyDictionary<Guid, PersonaDto>> ObtenerPersonasAsync(IEnumerable<Guid> usuarioIds, CancellationToken cancellationToken);

    // Plantillas: la vigente de un tipo es la de mayor versión.
    Task<PlantillaDocumento?> ObtenerPlantillaVigenteAsync(Guid tipoId, CancellationToken cancellationToken);

    void Agregar(PlantillaDocumento plantilla);

    // Documentos emitidos.
    void Agregar(DocumentoEmitido documento);

    /// <summary>El documento más reciente del trámite (vigente o anulado), o null si no se emitió ninguno.</summary>
    Task<DocumentoEmitido?> ObtenerDocumentoDeTramiteAsync(Guid tramiteId, CancellationToken cancellationToken);

    Task<DocumentoEmitido?> ObtenerDocumentoPorCodigoAsync(string codigoValidacion, CancellationToken cancellationToken);

    Task<bool> ExisteCodigoValidacionAsync(string codigoValidacion, CancellationToken cancellationToken);
}
