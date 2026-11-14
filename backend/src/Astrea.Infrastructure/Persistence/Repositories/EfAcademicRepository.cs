using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Academic;
using Astrea.Domain.Academic;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Infrastructure.Persistence.Repositories;

internal sealed class EfAcademicRepository(AppDbContext db) : IAcademicRepository
{
    public Task<bool> ExisteCarreraConCodigoAsync(string codigo, CancellationToken cancellationToken) =>
        db.Carreras.AnyAsync(carrera => carrera.Codigo == codigo, cancellationToken);

    public Task<Carrera?> ObtenerCarreraAsync(Guid carreraId, CancellationToken cancellationToken) =>
        db.Carreras.SingleOrDefaultAsync(carrera => carrera.Id == carreraId, cancellationToken);

    public Task<PlanEstudio?> ObtenerPlanAsync(Guid planId, CancellationToken cancellationToken) =>
        db.PlanesEstudio.Include(plan => plan.Materias).SingleOrDefaultAsync(plan => plan.Id == planId, cancellationToken);

    public Task<Materia?> ObtenerMateriaAsync(Guid materiaId, CancellationToken cancellationToken) =>
        db.Materias.SingleOrDefaultAsync(materia => materia.Id == materiaId, cancellationToken);

    public Task<bool> ExisteMateriaConCodigoAsync(string codigo, CancellationToken cancellationToken) =>
        db.Materias.AnyAsync(materia => materia.Codigo == codigo, cancellationToken);

    public void Agregar(Carrera carrera) => db.Carreras.Add(carrera);

    public void Agregar(PlanEstudio plan) => db.PlanesEstudio.Add(plan);

    public void Agregar(Materia materia) => db.Materias.Add(materia);

    public async Task<PagedResult<CarreraDto>> ListarCarrerasAsync(PageRequest pagina, bool incluirInactivas, CancellationToken cancellationToken)
    {
        var consulta = db.Carreras.AsNoTracking().Where(carrera => incluirInactivas || carrera.Activa);

        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderBy(carrera => carrera.Nombre)
            .ThenBy(carrera => carrera.Id)
            .Skip(pagina.Skip)
            .Take(pagina.PageSize)
            .Select(carrera => new CarreraDto(carrera.Id, carrera.Codigo, carrera.Nombre, carrera.DuracionAnios, carrera.Activa))
            .ToListAsync(cancellationToken);

        return PagedResult<CarreraDto>.From(items, pagina, total);
    }

    public async Task<IReadOnlyList<PlanEstudioDto>> ListarPlanesAsync(Guid carreraId, bool incluirInactivos, CancellationToken cancellationToken) =>
        await db.PlanesEstudio.AsNoTracking()
            .Where(plan => plan.CarreraId == carreraId && (incluirInactivos || plan.Activo))
            .OrderByDescending(plan => plan.Activo)
            .ThenByDescending(plan => plan.VigenciaDesde)
            .ThenBy(plan => plan.Id)
            .Select(plan => new PlanEstudioDto(
                plan.Id,
                plan.CarreraId,
                plan.CodigoVersion,
                plan.Nombre,
                plan.VigenciaDesde,
                plan.VigenciaHasta,
                plan.Activo,
                plan.Materias.Count))
            .ToListAsync(cancellationToken);

    public async Task<PagedResult<MateriaDePlanDto>> ListarMateriasDePlanAsync(
        Guid planId,
        int? anio,
        PageRequest pagina,
        bool incluirInactivas,
        CancellationToken cancellationToken)
    {
        var consulta =
            from planMateria in db.PlanMaterias.AsNoTracking()
            join materia in db.Materias.AsNoTracking() on planMateria.MateriaId equals materia.Id
            where planMateria.PlanEstudioId == planId
                && (anio == null || planMateria.AnioEnPlan == anio)
                && (incluirInactivas || materia.Activa)
            select new { planMateria, materia };

        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderBy(fila => fila.planMateria.AnioEnPlan)
            .ThenBy(fila => fila.planMateria.Orden)
            .ThenBy(fila => fila.planMateria.Id)
            .Skip(pagina.Skip)
            .Take(pagina.PageSize)
            .Select(fila => new MateriaDePlanDto(
                fila.planMateria.Id,
                fila.materia.Id,
                fila.materia.Codigo,
                fila.materia.Nombre,
                fila.planMateria.AnioEnPlan,
                fila.planMateria.Cuatrimestre,
                fila.planMateria.Orden,
                fila.planMateria.CargaHorariaSemanal,
                fila.planMateria.Obligatoria,
                fila.materia.Activa))
            .ToListAsync(cancellationToken);

        return PagedResult<MateriaDePlanDto>.From(items, pagina, total);
    }
}
