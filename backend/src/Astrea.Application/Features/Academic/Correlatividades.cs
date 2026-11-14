using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Results;
using Astrea.Domain.Academic;

namespace Astrea.Application.Features.Academic;

public sealed record AgregarCorrelatividadCommand(
    Guid MateriaDestinoId,
    TipoCorrelatividad Tipo,
    Guid? MateriaRequisitoId,
    CondicionRequisito Condicion,
    int? GrupoAlternativa,
    string? Descripcion);

public sealed record EstadoDeMateria(Guid MateriaId, EstadoMateria Estado);

/// <summary>Situación hipotética o declarada por el estudiante; Astrea todavía no guarda su historial académico.</summary>
public sealed record VerificarCorrelatividadesCommand(TipoCorrelatividad Tipo, IReadOnlyList<EstadoDeMateria>? Estados);

/// <summary>
/// Consulta, verificación y administración de correlatividades de un plan.
/// Las materias se identifican por <c>MateriaId</c>, como en la API pública.
/// </summary>
public sealed class Correlatividades(IAcademicRepository academico, IUnitOfWork unitOfWork)
{
    public async Task<Result<CorrelatividadesDeMateriaDto>> ObtenerAsync(Guid planId, Guid materiaId, bool incluirInactivas, CancellationToken cancellationToken)
    {
        var detalle = await academico.ObtenerCorrelatividadesAsync(planId, materiaId, incluirInactivas, cancellationToken);
        return detalle is null ? ErroresAcademicos.MateriaNoEncontrada : detalle;
    }

    public async Task<Result<VerificacionCorrelatividadesDto>> VerificarAsync(
        Guid planId,
        Guid materiaId,
        VerificarCorrelatividadesCommand comando,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(comando.Tipo))
        {
            return Error.Validacion("academico.tipo_invalido", "Indicá si querés verificar para cursar o para rendir.");
        }

        var detalle = await academico.ObtenerCorrelatividadesAsync(planId, materiaId, incluirInactivas: false, cancellationToken);
        var plan = detalle is null ? null : await academico.ObtenerPlanAsync(planId, cancellationToken);
        if (detalle is null || plan is null)
        {
            return ErroresAcademicos.MateriaNoEncontrada;
        }

        // Estados por PlanMateria; las materias que no son del plan se ignoran.
        var planMateriaPorMateria = plan.Materias.ToDictionary(planMateria => planMateria.MateriaId, planMateria => planMateria.Id);
        var estados = new Dictionary<Guid, EstadoMateria>();
        foreach (var estado in comando.Estados ?? [])
        {
            if (planMateriaPorMateria.TryGetValue(estado.MateriaId, out var planMateriaId) && Enum.IsDefined(estado.Estado))
            {
                estados[planMateriaId] = estado.Estado;
            }
        }

        var verificacion = plan.VerificarCorrelatividades(detalle.PlanMateriaId, comando.Tipo, estados);
        var requisitos = (comando.Tipo == TipoCorrelatividad.Cursar ? detalle.ParaCursar : detalle.ParaRendir).ToDictionary(requisito => requisito.Id);

        return new VerificacionCorrelatividadesDto(
            comando.Tipo,
            verificacion.Resultado,
            verificacion.Faltantes.Select(regla => requisitos[regla.Id]).ToList(),
            verificacion.AVerificar.Select(regla => requisitos[regla.Id]).ToList());
    }

    /// <summary>Agrega el requisito y devuelve todas las correlatividades de la materia destino.</summary>
    public async Task<Result<CorrelatividadesDeMateriaDto>> AgregarAsync(Guid planId, AgregarCorrelatividadCommand comando, CancellationToken cancellationToken)
    {
        var plan = await academico.ObtenerPlanAsync(planId, cancellationToken);
        if (plan is null)
        {
            return ErroresAcademicos.PlanNoEncontrado;
        }

        var destino = plan.Materias.SingleOrDefault(planMateria => planMateria.MateriaId == comando.MateriaDestinoId);
        var requisito = comando.MateriaRequisitoId is Guid requisitoId
            ? plan.Materias.SingleOrDefault(planMateria => planMateria.MateriaId == requisitoId)
            : null;
        if (destino is null || (comando.MateriaRequisitoId is not null && requisito is null))
        {
            return ErroresAcademicos.MateriaNoEncontrada;
        }

        plan.AgregarCorrelatividad(
            Guid.CreateVersion7(),
            destino.Id,
            comando.Tipo,
            requisito?.Id,
            comando.Condicion,
            comando.GrupoAlternativa,
            comando.Descripcion);

        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return await ObtenerAsync(planId, comando.MateriaDestinoId, incluirInactivas: true, cancellationToken);
    }

    public async Task<Result<bool>> QuitarAsync(Guid planId, Guid correlatividadId, CancellationToken cancellationToken)
    {
        var plan = await academico.ObtenerPlanAsync(planId, cancellationToken);
        if (plan is null || plan.Correlatividades.All(correlatividad => correlatividad.Id != correlatividadId))
        {
            return ErroresAcademicos.CorrelatividadNoEncontrada;
        }

        plan.QuitarCorrelatividad(correlatividadId);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return true;
    }
}
