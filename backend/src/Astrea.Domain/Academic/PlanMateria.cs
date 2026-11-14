using Astrea.Domain.Common;

namespace Astrea.Domain.Academic;

public enum Cuatrimestre
{
    Anual = 0,
    Primero = 1,
    Segundo = 2,
}

/// <summary>Pertenencia de una materia a un plan: año, cuatrimestre y orden dentro del año.</summary>
public sealed class PlanMateria : Entity
{
    private PlanMateria()
    {
    }

    internal PlanMateria(Guid id, Guid planEstudioId, Guid materiaId, int anioEnPlan, Cuatrimestre cuatrimestre, int orden, int? cargaHorariaSemanal, bool obligatoria)
        : base(id)
    {
        PlanEstudioId = planEstudioId;
        MateriaId = materiaId;
        Orden = orden;
        Asignar(anioEnPlan, cuatrimestre, cargaHorariaSemanal, obligatoria);
    }

    public Guid PlanEstudioId { get; private set; }

    public Guid MateriaId { get; private set; }

    public int AnioEnPlan { get; private set; }

    public Cuatrimestre Cuatrimestre { get; private set; }

    public int Orden { get; private set; }

    public int? CargaHorariaSemanal { get; private set; }

    public bool Obligatoria { get; private set; }

    internal void Asignar(int anioEnPlan, Cuatrimestre cuatrimestre, int? cargaHorariaSemanal, bool obligatoria)
    {
        if (anioEnPlan is < 1 or > Carrera.DuracionMaximaAnios)
        {
            throw new DomainException($"El año de la materia debe estar entre 1 y {Carrera.DuracionMaximaAnios}.");
        }

        if (!Enum.IsDefined(cuatrimestre))
        {
            throw new DomainException("El cuatrimestre no es válido.");
        }

        if (cargaHorariaSemanal is < 1 or > 40)
        {
            throw new DomainException("La carga horaria semanal debe estar entre 1 y 40 horas.");
        }

        AnioEnPlan = anioEnPlan;
        Cuatrimestre = cuatrimestre;
        CargaHorariaSemanal = cargaHorariaSemanal;
        Obligatoria = obligatoria;
    }
}
