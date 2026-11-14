using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Results;
using Astrea.Domain.Academic;

namespace Astrea.Application.Features.Academic;

public sealed record GuardarCarreraCommand(string Codigo, string Nombre, int DuracionAnios, bool Activa = true);

public sealed record GuardarPlanCommand(string CodigoVersion, string Nombre, DateOnly? VigenciaDesde, DateOnly? VigenciaHasta, bool Activo = true);

public sealed record AgregarMateriaCommand(string Nombre, int Anio, Cuatrimestre Cuatrimestre, int? CargaHorariaSemanal, bool Obligatoria = true);

public sealed record ActualizarMateriaCommand(string Nombre, int Anio, Cuatrimestre Cuatrimestre, int? CargaHorariaSemanal, bool Obligatoria, bool Activa);

/// <summary>
/// Altas y modificaciones del plan de estudios (solo Administrador).
/// Las reglas de validación viven en el dominio; si se violan, el dominio lanza DomainException (400).
/// </summary>
public sealed class AdministracionCatalogo(IAcademicRepository academico, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<Result<CarreraDto>> CrearCarreraAsync(GuardarCarreraCommand comando, CancellationToken cancellationToken)
    {
        var carrera = Carrera.Crear(Guid.CreateVersion7(), comando.Codigo, comando.Nombre, comando.DuracionAnios, clock.UtcNow);

        if (await academico.ExisteCarreraConCodigoAsync(carrera.Codigo, cancellationToken))
        {
            return ErroresAcademicos.CodigoCarreraDuplicado(carrera.Codigo);
        }

        academico.Agregar(carrera);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return ADto(carrera);
    }

    /// <summary>El código no se modifica: identifica a la carrera en documentos y reportes.</summary>
    public async Task<Result<CarreraDto>> ActualizarCarreraAsync(Guid carreraId, GuardarCarreraCommand comando, CancellationToken cancellationToken)
    {
        var carrera = await academico.ObtenerCarreraAsync(carreraId, cancellationToken);
        if (carrera is null)
        {
            return ErroresAcademicos.CarreraNoEncontrada;
        }

        carrera.Actualizar(comando.Nombre, comando.DuracionAnios, comando.Activa, clock.UtcNow);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return ADto(carrera);
    }

    public async Task<Result<PlanEstudioDto>> CrearPlanAsync(Guid carreraId, GuardarPlanCommand comando, CancellationToken cancellationToken)
    {
        var carrera = await academico.ObtenerCarreraAsync(carreraId, cancellationToken);
        if (carrera is null)
        {
            return ErroresAcademicos.CarreraNoEncontrada;
        }

        var plan = PlanEstudio.Crear(Guid.CreateVersion7(), carrera, comando.CodigoVersion, comando.Nombre, comando.VigenciaDesde, comando.VigenciaHasta);
        academico.Agregar(plan);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return ADto(plan);
    }

    public async Task<Result<PlanEstudioDto>> ActualizarPlanAsync(Guid planId, GuardarPlanCommand comando, CancellationToken cancellationToken)
    {
        var plan = await academico.ObtenerPlanAsync(planId, cancellationToken);
        if (plan is null)
        {
            return ErroresAcademicos.PlanNoEncontrado;
        }

        plan.Actualizar(comando.CodigoVersion, comando.Nombre, comando.VigenciaDesde, comando.VigenciaHasta, comando.Activo);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return ADto(plan);
    }

    /// <summary>Crea la materia y la ubica en el plan. El código se genera a partir de la carrera.</summary>
    public async Task<Result<MateriaDePlanDto>> AgregarMateriaAsync(Guid planId, AgregarMateriaCommand comando, CancellationToken cancellationToken)
    {
        var plan = await academico.ObtenerPlanAsync(planId, cancellationToken);
        if (plan is null)
        {
            return ErroresAcademicos.PlanNoEncontrado;
        }

        var carrera = await academico.ObtenerCarreraAsync(plan.CarreraId, cancellationToken);
        var codigo = await GenerarCodigoMateriaAsync(carrera!.Codigo, plan.Materias.Count + 1, cancellationToken);

        var materia = Materia.Crear(Guid.CreateVersion7(), codigo, comando.Nombre);
        var planMateria = plan.AgregarMateria(Guid.CreateVersion7(), materia, comando.Anio, comando.Cuatrimestre, comando.CargaHorariaSemanal, comando.Obligatoria);

        academico.Agregar(materia);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return ADto(planMateria, materia);
    }

    public async Task<Result<MateriaDePlanDto>> ActualizarMateriaAsync(
        Guid planId,
        Guid planMateriaId,
        ActualizarMateriaCommand comando,
        CancellationToken cancellationToken)
    {
        var plan = await academico.ObtenerPlanAsync(planId, cancellationToken);
        var planMateria = plan?.Materias.SingleOrDefault(existente => existente.Id == planMateriaId);
        if (plan is null || planMateria is null)
        {
            return ErroresAcademicos.MateriaNoEncontrada;
        }

        var materia = (await academico.ObtenerMateriaAsync(planMateria.MateriaId, cancellationToken))!;
        materia.Actualizar(comando.Nombre, comando.Activa);
        plan.ActualizarMateria(planMateriaId, comando.Anio, comando.Cuatrimestre, comando.CargaHorariaSemanal, comando.Obligatoria);

        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return ADto(planMateria, materia);
    }

    private async Task<string> GenerarCodigoMateriaAsync(string codigoCarrera, int secuencia, CancellationToken cancellationToken)
    {
        string codigo;
        do
        {
            codigo = $"{codigoCarrera}-{secuencia++:000}";
        }
        while (await academico.ExisteMateriaConCodigoAsync(codigo, cancellationToken));

        return codigo;
    }

    private static CarreraDto ADto(Carrera carrera) =>
        new(carrera.Id, carrera.Codigo, carrera.Nombre, carrera.DuracionAnios, carrera.Activa);

    private static PlanEstudioDto ADto(PlanEstudio plan) =>
        new(plan.Id, plan.CarreraId, plan.CodigoVersion, plan.Nombre, plan.VigenciaDesde, plan.VigenciaHasta, plan.Activo, plan.Materias.Count);

    private static MateriaDePlanDto ADto(PlanMateria planMateria, Materia materia) =>
        new(
            planMateria.Id,
            materia.Id,
            materia.Codigo,
            materia.Nombre,
            planMateria.AnioEnPlan,
            planMateria.Cuatrimestre,
            planMateria.Orden,
            planMateria.CargaHorariaSemanal,
            planMateria.Obligatoria,
            materia.Activa);
}
