using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Knowledge;
using Astrea.Application.Features.Procedures;
using Astrea.Domain.Knowledge;

namespace Astrea.Application.Abstractions.Persistence;

public interface IDocumentoRagRepository
{
    Task<DocumentoRag?> ObtenerAsync(Guid id, CancellationToken cancellationToken);

    void Agregar(DocumentoRag documento);

    /// <summary>La versión más reciente de cada documento (una fila por serie), filtrada y paginada.</summary>
    Task<PagedResult<DocumentoRag>> ListarAsync(FiltroDocumentosRag filtro, PageRequest pagina, CancellationToken cancellationToken);

    /// <summary>Todas las versiones de un documento, de la más nueva a la más vieja.</summary>
    Task<IReadOnlyList<DocumentoRag>> ObtenerSerieAsync(Guid serieId, CancellationToken cancellationToken);

    /// <summary>Un documento no retirado con exactamente el mismo archivo, si existe.</summary>
    Task<DocumentoRag?> BuscarPorHashAsync(string hashSha256, CancellationToken cancellationToken);

    /// <summary>El pendiente más antiguo, listo para modificar.</summary>
    Task<DocumentoRag?> ObtenerSiguientePendienteAsync(CancellationToken cancellationToken);

    /// <summary>Los que quedaron "Procesando" (el proceso se cortó), listos para modificar.</summary>
    Task<IReadOnlyList<DocumentoRag>> ListarProcesandoAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Guarda en una sola transacción los fragmentos nuevos del documento (borrando los que tuviera) junto con los
    /// cambios de estado pendientes. Si algo falla no queda nada a medias.
    /// </summary>
    Task GuardarIndexacionAsync(Guid documentoId, IReadOnlyList<FragmentoRag> fragmentos, CancellationToken cancellationToken);

    Task<PagedResult<FragmentoDto>> ListarFragmentosAsync(Guid documentoId, PageRequest pagina, CancellationToken cancellationToken);

    /// <summary>Los <paramref name="cantidad"/> fragmentos más parecidos (similitud coseno) entre los documentos indexados.</summary>
    Task<IReadOnlyList<FragmentoEncontrado>> BuscarSimilaresAsync(float[] consulta, int cantidad, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, PersonaDto>> ObtenerPersonasAsync(IEnumerable<Guid> usuarioIds, CancellationToken cancellationToken);
}
