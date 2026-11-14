using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Academic;
using Astrea.Domain.Academic;

namespace Astrea.Application.Abstractions.Persistence;

public interface IAcademicRepository
{
    Task<bool> ExisteCarreraConCodigoAsync(string codigo, CancellationToken cancellationToken);

    Task<Carrera?> ObtenerCarreraAsync(Guid carreraId, CancellationToken cancellationToken);

    /// <summary>Plan con sus materias y correlatividades cargadas, listo para modificar.</summary>
    Task<PlanEstudio?> ObtenerPlanAsync(Guid planId, CancellationToken cancellationToken);

    Task<Materia?> ObtenerMateriaAsync(Guid materiaId, CancellationToken cancellationToken);

    Task<bool> ExisteMateriaConCodigoAsync(string codigo, CancellationToken cancellationToken);

    void Agregar(Carrera carrera);

    void Agregar(PlanEstudio plan);

    void Agregar(Materia materia);

    // Consultas de lectura: devuelven DTOs, ordenadas de forma estable.
    Task<PagedResult<CarreraDto>> ListarCarrerasAsync(PageRequest pagina, bool incluirInactivas, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanEstudioDto>> ListarPlanesAsync(Guid carreraId, bool incluirInactivos, CancellationToken cancellationToken);

    Task<PagedResult<MateriaDePlanDto>> ListarMateriasDePlanAsync(Guid planId, int? anio, PageRequest pagina, bool incluirInactivas, CancellationToken cancellationToken);

    /// <summary>
    /// Requisitos para cursar y rendir una materia del plan, ordenados por grupo, año y orden del requisito.
    /// Devuelve null si el plan o la materia no existen (o están inactivos y no se piden inactivos).
    /// </summary>
    Task<CorrelatividadesDeMateriaDto?> ObtenerCorrelatividadesAsync(Guid planId, Guid materiaId, bool incluirInactivas, CancellationToken cancellationToken);
}
