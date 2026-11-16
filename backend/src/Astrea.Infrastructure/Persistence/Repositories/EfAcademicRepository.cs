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
        db.PlanesEstudio
            .Include(plan => plan.Materias)
            .Include(plan => plan.Correlatividades)
            .AsSplitQuery()
            .SingleOrDefaultAsync(plan => plan.Id == planId, cancellationToken);

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

    public async Task<IReadOnlyList<MateriaEncontradaDto>> BuscarMateriasAsync(string texto, Guid? carreraId, int maximo, CancellationToken cancellationToken)
    {
        var limpio = texto.Trim();
        var patron = $"%{limpio.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("%", @"\%", StringComparison.Ordinal).Replace("_", @"\_", StringComparison.Ordinal)}%";
        var encontradas = await (
            from planMateria in db.PlanMaterias.AsNoTracking()
            join materia in db.Materias.AsNoTracking() on planMateria.MateriaId equals materia.Id
            join plan in db.PlanesEstudio.AsNoTracking() on planMateria.PlanEstudioId equals plan.Id
            join carrera in db.Carreras.AsNoTracking() on plan.CarreraId equals carrera.Id
            where materia.Activa && plan.Activo && carrera.Activa
                && (carreraId == null || carrera.Id == carreraId)
                && (EF.Functions.ILike(EF.Functions.Unaccent(materia.Nombre), EF.Functions.Unaccent(patron))
                    || EF.Functions.ILike(materia.Codigo, patron))
            orderby EF.Functions.ILike(EF.Functions.Unaccent(materia.Nombre), EF.Functions.Unaccent(limpio)) descending, materia.Nombre.Length, materia.Nombre, carrera.Nombre
            select new MateriaEncontradaDto(plan.Id, materia.Id, materia.Codigo, materia.Nombre, planMateria.AnioEnPlan, carrera.Id, carrera.Nombre, plan.Nombre))
            .Take(maximo)
            .ToListAsync(cancellationToken);
        return encontradas;
    }

    public async Task<CorrelatividadesDeMateriaDto?> ObtenerCorrelatividadesAsync(
        Guid planId,
        Guid materiaId,
        bool incluirInactivas,
        CancellationToken cancellationToken)
    {
        var destino = await (
            from planMateria in db.PlanMaterias.AsNoTracking()
            join materia in db.Materias.AsNoTracking() on planMateria.MateriaId equals materia.Id
            join plan in db.PlanesEstudio.AsNoTracking() on planMateria.PlanEstudioId equals plan.Id
            where planMateria.PlanEstudioId == planId
                && materia.Id == materiaId
                && (incluirInactivas || (materia.Activa && plan.Activo))
            select new { planMateria.Id, materia.Codigo, materia.Nombre, planMateria.AnioEnPlan })
            .SingleOrDefaultAsync(cancellationToken);

        if (destino is null)
        {
            return null;
        }

        var reglas = await db.Correlatividades.AsNoTracking()
            .Where(regla => regla.PlanMateriaDestinoId == destino.Id)
            .ToListAsync(cancellationToken);

        var idsRequisitos = reglas
            .Where(regla => regla.PlanMateriaRequisitoId != null)
            .Select(regla => regla.PlanMateriaRequisitoId!.Value)
            .Distinct()
            .ToList();

        var requisitos = await (
            from planMateria in db.PlanMaterias.AsNoTracking()
            join materia in db.Materias.AsNoTracking() on planMateria.MateriaId equals materia.Id
            where idsRequisitos.Contains(planMateria.Id)
            select new { planMateria.Id, MateriaId = materia.Id, materia.Codigo, materia.Nombre, planMateria.AnioEnPlan, planMateria.Orden })
            .ToDictionaryAsync(fila => fila.Id, cancellationToken);

        // Primero los obligatorios por año y orden del plan; los requisitos en texto al final; luego cada grupo de alternativas.
        List<RequisitoDto> Requisitos(TipoCorrelatividad tipo) => reglas
            .Where(regla => regla.Tipo == tipo)
            .Select(regla => (regla, materia: regla.PlanMateriaRequisitoId is Guid id ? requisitos[id] : null))
            .OrderBy(fila => fila.regla.GrupoAlternativa ?? 0)
            .ThenBy(fila => fila.materia is null)
            .ThenBy(fila => fila.materia?.AnioEnPlan)
            .ThenBy(fila => fila.materia?.Orden)
            .ThenBy(fila => fila.regla.Descripcion, StringComparer.Ordinal)
            .Select(fila => new RequisitoDto(
                fila.regla.Id,
                fila.regla.Condicion,
                fila.regla.GrupoAlternativa,
                fila.materia?.MateriaId,
                fila.materia?.Codigo,
                fila.materia?.Nombre,
                fila.materia?.AnioEnPlan,
                fila.regla.Descripcion))
            .ToList();

        return new CorrelatividadesDeMateriaDto(
            planId,
            destino.Id,
            materiaId,
            destino.Codigo,
            destino.Nombre,
            destino.AnioEnPlan,
            Requisitos(TipoCorrelatividad.Cursar),
            Requisitos(TipoCorrelatividad.Rendir));
    }
}
