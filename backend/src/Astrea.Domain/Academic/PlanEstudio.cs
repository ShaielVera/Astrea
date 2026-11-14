using Astrea.Domain.Common;

namespace Astrea.Domain.Academic;

/// <summary>
/// Versión del plan de estudios de una carrera (ej. "Res. 2120/16"). Un cambio curricular
/// se carga como plan nuevo para no modificar retroactivamente a las cohortes anteriores.
/// </summary>
public sealed class PlanEstudio : Entity
{
    private readonly List<PlanMateria> _materias = [];

    private PlanEstudio()
    {
    }

    private PlanEstudio(Guid id, Guid carreraId, int duracionAnios)
        : base(id)
    {
        CarreraId = carreraId;
        DuracionAnios = duracionAnios;
        Activo = true;
    }

    public Guid CarreraId { get; private set; }

    public string CodigoVersion { get; private set; } = null!;

    public string Nombre { get; private set; } = null!;

    /// <summary>Copia de la duración de la carrera al crear el plan; limita el año de las materias.</summary>
    public int DuracionAnios { get; private set; }

    public DateOnly? VigenciaDesde { get; private set; }

    public DateOnly? VigenciaHasta { get; private set; }

    public bool Activo { get; private set; }

    public IReadOnlyCollection<PlanMateria> Materias => _materias.AsReadOnly();

    public static PlanEstudio Crear(Guid id, Carrera carrera, string codigoVersion, string nombre, DateOnly? vigenciaDesde, DateOnly? vigenciaHasta)
    {
        ArgumentNullException.ThrowIfNull(carrera);

        var plan = new PlanEstudio(id, carrera.Id, carrera.DuracionAnios);
        plan.Actualizar(codigoVersion, nombre, vigenciaDesde, vigenciaHasta, activo: true);
        return plan;
    }

    public void Actualizar(string codigoVersion, string nombre, DateOnly? vigenciaDesde, DateOnly? vigenciaHasta, bool activo)
    {
        if (vigenciaDesde is not null && vigenciaHasta is not null && vigenciaHasta < vigenciaDesde)
        {
            throw new DomainException("La vigencia del plan no puede terminar antes de empezar.");
        }

        CodigoVersion = Texto.Requerido(codigoVersion, "código de versión del plan", 50);
        Nombre = Texto.Requerido(nombre, "nombre del plan", 200);
        VigenciaDesde = vigenciaDesde;
        VigenciaHasta = vigenciaHasta;
        Activo = activo;
    }

    /// <summary>Agrega la materia al final del año indicado.</summary>
    public PlanMateria AgregarMateria(Guid planMateriaId, Materia materia, int anio, Cuatrimestre cuatrimestre, int? cargaHorariaSemanal, bool obligatoria)
    {
        ArgumentNullException.ThrowIfNull(materia);
        ValidarAnio(anio);

        if (_materias.Any(existente => existente.MateriaId == materia.Id))
        {
            throw new DomainException("La materia ya forma parte de este plan.");
        }

        var orden = _materias.Where(existente => existente.AnioEnPlan == anio).Select(existente => existente.Orden).DefaultIfEmpty(0).Max() + 1;
        var planMateria = new PlanMateria(planMateriaId, Id, materia.Id, anio, cuatrimestre, orden, cargaHorariaSemanal, obligatoria);
        _materias.Add(planMateria);
        return planMateria;
    }

    public void ActualizarMateria(Guid planMateriaId, int anio, Cuatrimestre cuatrimestre, int? cargaHorariaSemanal, bool obligatoria)
    {
        var planMateria = _materias.SingleOrDefault(existente => existente.Id == planMateriaId)
            ?? throw new DomainException("La materia no forma parte de este plan.");

        ValidarAnio(anio);
        planMateria.Asignar(anio, cuatrimestre, cargaHorariaSemanal, obligatoria);
    }

    private void ValidarAnio(int anio)
    {
        if (anio < 1 || anio > DuracionAnios)
        {
            throw new DomainException($"El año debe estar entre 1 y {DuracionAnios}, la duración de la carrera.");
        }
    }
}
