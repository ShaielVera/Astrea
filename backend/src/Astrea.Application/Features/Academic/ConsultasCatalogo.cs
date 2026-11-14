using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Contracts.Results;

namespace Astrea.Application.Features.Academic;

/// <summary>
/// Consultas del catálogo académico. Los estudiantes ven solo lo activo;
/// el panel de administración pasa <c>incluirInactivos = true</c>.
/// </summary>
public sealed class ConsultasCatalogo(IAcademicRepository academico)
{
    public Task<PagedResult<CarreraDto>> ListarCarrerasAsync(int? page, int? pageSize, bool incluirInactivas, CancellationToken cancellationToken) =>
        academico.ListarCarrerasAsync(PageRequest.Create(page, pageSize), incluirInactivas, cancellationToken);

    public async Task<Result<IReadOnlyList<PlanEstudioDto>>> ListarPlanesAsync(Guid carreraId, bool incluirInactivos, CancellationToken cancellationToken)
    {
        var carrera = await academico.ObtenerCarreraAsync(carreraId, cancellationToken);
        if (carrera is null || (!carrera.Activa && !incluirInactivos))
        {
            return ErroresAcademicos.CarreraNoEncontrada;
        }

        var planes = await academico.ListarPlanesAsync(carreraId, incluirInactivos, cancellationToken);
        return Result<IReadOnlyList<PlanEstudioDto>>.Exito(planes);
    }

    public async Task<Result<PagedResult<MateriaDePlanDto>>> ListarMateriasAsync(
        Guid planId,
        int? anio,
        int? page,
        int? pageSize,
        bool incluirInactivas,
        CancellationToken cancellationToken)
    {
        var plan = await academico.ObtenerPlanAsync(planId, cancellationToken);
        if (plan is null || (!plan.Activo && !incluirInactivas))
        {
            return ErroresAcademicos.PlanNoEncontrado;
        }

        return await academico.ListarMateriasDePlanAsync(planId, anio, PageRequest.Create(page, pageSize), incluirInactivas, cancellationToken);
    }
}
