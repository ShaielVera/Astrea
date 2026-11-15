using Astrea.Domain.Common;

namespace Astrea.Domain.Academic;

/// <summary>Para qué se exige el requisito: inscribirse a cursar o rendir el final.</summary>
public enum TipoCorrelatividad
{
    Cursar = 1,
    Rendir = 2,
}

/// <summary>Condición que debe tener el requisito. Regularizada no equivale a Aprobada.</summary>
public enum CondicionRequisito
{
    Regularizada = 1,
    Aprobada = 2,
}

/// <summary>Situación de un estudiante en una materia, usada para verificar correlatividades.</summary>
public enum EstadoMateria
{
    SinCursar = 0,
    Regularizada = 1,
    Aprobada = 2,
}

/// <summary>
/// Requisito para cursar o rendir una materia del plan.
/// <para>
/// Semántica de las reglas de una materia y un tipo:
/// los requisitos sin <see cref="GrupoAlternativa"/> son obligatorios (Y lógico);
/// los que comparten el mismo número de grupo son alternativas entre sí (alcanza con uno, O lógico),
/// y cada grupo, como conjunto, también es obligatorio.
/// </para>
/// <para>
/// Un requisito sin materia (<see cref="PlanMateriaRequisitoId"/> nulo) es una condición descrita en texto,
/// por ejemplo "todas las unidades curriculares de primer año aprobadas"; Secretaría la verifica manualmente.
/// </para>
/// </summary>
public sealed class Correlatividad : Entity
{
    public const int GrupoMaximo = 20;

    private Correlatividad()
    {
    }

    internal Correlatividad(
        Guid id,
        Guid planEstudioId,
        Guid planMateriaDestinoId,
        TipoCorrelatividad tipo,
        Guid? planMateriaRequisitoId,
        CondicionRequisito condicion,
        int? grupoAlternativa,
        string? descripcion)
        : base(id)
    {
        if (!Enum.IsDefined(tipo))
        {
            throw new DomainException("El tipo de correlatividad no es válido.");
        }

        if (!Enum.IsDefined(condicion))
        {
            throw new DomainException("La condición del requisito no es válida.");
        }

        if (grupoAlternativa is < 1 or > GrupoMaximo)
        {
            throw new DomainException($"El grupo de alternativas debe estar entre 1 y {GrupoMaximo}.");
        }

        if (planMateriaRequisitoId == planMateriaDestinoId)
        {
            throw new DomainException("Una materia no puede ser correlativa de sí misma.");
        }

        Descripcion = planMateriaRequisitoId is null
            ? Texto.Requerido(descripcion, "texto del requisito", 300)
            : Texto.Opcional(descripcion, "texto de la observación", 300);

        PlanEstudioId = planEstudioId;
        PlanMateriaDestinoId = planMateriaDestinoId;
        Tipo = tipo;
        PlanMateriaRequisitoId = planMateriaRequisitoId;
        Condicion = condicion;
        GrupoAlternativa = grupoAlternativa;
    }

    public Guid PlanEstudioId { get; private set; }

    public Guid PlanMateriaDestinoId { get; private set; }

    public TipoCorrelatividad Tipo { get; private set; }

    public Guid? PlanMateriaRequisitoId { get; private set; }

    public CondicionRequisito Condicion { get; private set; }

    public int? GrupoAlternativa { get; private set; }

    public string? Descripcion { get; private set; }

    public bool RequiereVerificacionManual => PlanMateriaRequisitoId is null;

    /// <summary>Una materia aprobada también cumple el requisito de regularizada; al revés, no.</summary>
    public bool SeCumpleCon(EstadoMateria estado) => Condicion switch
    {
        CondicionRequisito.Regularizada => estado is EstadoMateria.Regularizada or EstadoMateria.Aprobada,
        CondicionRequisito.Aprobada => estado is EstadoMateria.Aprobada,
        _ => false,
    };
}

public enum ResultadoVerificacion
{
    Cumple = 1,
    NoCumple = 2,

    /// <summary>Se cumplen los requisitos de materias, pero quedan condiciones que verifica Secretaría.</summary>
    RequiereVerificacion = 3,
}

public sealed record VerificacionCorrelatividades(
    ResultadoVerificacion Resultado,
    IReadOnlyList<Correlatividad> Faltantes,
    IReadOnlyList<Correlatividad> AVerificar);

/// <summary>Aplica la semántica documentada en <see cref="Correlatividad"/> a la situación de un estudiante.</summary>
public static class VerificadorCorrelatividades
{
    public static VerificacionCorrelatividades Verificar(
        IEnumerable<Correlatividad> reglas,
        IReadOnlyDictionary<Guid, EstadoMateria> estados)
    {
        ArgumentNullException.ThrowIfNull(reglas);
        ArgumentNullException.ThrowIfNull(estados);

        var faltantes = new List<Correlatividad>();
        var aVerificar = new List<Correlatividad>();

        // Cada requisito obligatorio es su propio grupo; los alternativos se agrupan por número.
        var grupos = reglas
            .GroupBy(regla => regla.GrupoAlternativa is int grupo ? $"g{grupo}" : $"r{regla.Id}")
            .Select(grupo => grupo.ToList());

        foreach (var grupo in grupos)
        {
            var materias = grupo.Where(regla => !regla.RequiereVerificacionManual).ToList();
            var manuales = grupo.Where(regla => regla.RequiereVerificacionManual).ToList();

            if (materias.Any(regla => regla.SeCumpleCon(Estado(estados, regla.PlanMateriaRequisitoId!.Value))))
            {
                continue;
            }

            if (manuales.Count > 0)
            {
                aVerificar.AddRange(manuales);
            }
            else
            {
                faltantes.AddRange(materias);
            }
        }

        var resultado = faltantes.Count > 0
            ? ResultadoVerificacion.NoCumple
            : aVerificar.Count > 0 ? ResultadoVerificacion.RequiereVerificacion : ResultadoVerificacion.Cumple;

        return new VerificacionCorrelatividades(resultado, faltantes, aVerificar);
    }

    private static EstadoMateria Estado(IReadOnlyDictionary<Guid, EstadoMateria> estados, Guid planMateriaId) =>
        estados.TryGetValue(planMateriaId, out var estado) ? estado : EstadoMateria.SinCursar;
}
