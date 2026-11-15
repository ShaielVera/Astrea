using Astrea.Domain.Common;

namespace Astrea.Domain.Exams;

/// <summary>
/// Ciclo de vida de una mesa: se carga como borrador, se publica para que la vean los estudiantes
/// y, si no se toma, se cancela con un motivo (sigue visible para que nadie se presente).
/// </summary>
public enum EstadoMesa
{
    Borrador = 1,
    Publicada = 2,
    Cancelada = 3,
}

/// <summary>Situación de la inscripción a una mesa respecto del día de hoy (hora del instituto).</summary>
public enum EstadoInscripcion
{
    SinFechas = 0,
    Proxima = 1,
    Abierta = 2,
    Cerrada = 3,
}

/// <summary>
/// Llamado a examen final de una materia de un plan. Fecha y hora se expresan en la hora oficial
/// del instituto (<see cref="ZonaHorariaInstitucional"/>), que es como las publica Bedelía.
/// </summary>
public sealed class MesaExamen : Entity
{
    public const int MaximoLlamado = 5;
    public const int MaximoIntegrantesTribunal = 5;

    private List<string> _tribunal = [];

    private MesaExamen()
    {
    }

    private MesaExamen(Guid id, Guid planMateriaId, DateTimeOffset ahora)
        : base(id)
    {
        PlanMateriaId = planMateriaId;
        Estado = EstadoMesa.Borrador;
        CreatedAt = ahora;
        UpdatedAt = ahora;
    }

    public Guid PlanMateriaId { get; private set; }

    /// <summary>Turno de exámenes, tal como lo nombra el instituto (ej. "Diciembre 2026").</summary>
    public string Turno { get; private set; } = null!;

    /// <summary>Número de llamado dentro del turno (1.º, 2.º…).</summary>
    public int Llamado { get; private set; }

    public DateOnly Fecha { get; private set; }

    public TimeOnly Hora { get; private set; }

    public string? Aula { get; private set; }

    /// <summary>Integrantes del tribunal; el primero es quien lo preside.</summary>
    public IReadOnlyList<string> Tribunal => _tribunal;

    public DateOnly? InscripcionDesde { get; private set; }

    public DateOnly? InscripcionHasta { get; private set; }

    public string? Observaciones { get; private set; }

    public EstadoMesa Estado { get; private set; }

    public string? MotivoCancelacion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? PublicadaAt { get; private set; }

    public DateTimeOffset? CanceladaAt { get; private set; }

    /// <summary>Inicio del examen como instante absoluto (con el desfase horario del instituto).</summary>
    public DateTimeOffset Inicio => ZonaHorariaInstitucional.AInstante(Fecha, Hora);

    public static MesaExamen Crear(Guid id, Guid planMateriaId, DatosMesa datos, DateTimeOffset ahora)
    {
        if (planMateriaId == Guid.Empty)
        {
            throw new DomainException("La mesa debe indicar la materia.");
        }

        var mesa = new MesaExamen(id, planMateriaId, ahora);
        mesa.Asignar(datos);
        return mesa;
    }

    /// <summary>
    /// Corrige los datos de la mesa. Una mesa cancelada ya no se modifica.
    /// Una mesa publicada no puede pasar a una fecha que ya pasó.
    /// </summary>
    public void Actualizar(Guid planMateriaId, DatosMesa datos, DateTimeOffset ahora)
    {
        if (Estado == EstadoMesa.Cancelada)
        {
            throw new DomainException("Una mesa cancelada no se puede modificar.");
        }

        if (planMateriaId == Guid.Empty)
        {
            throw new DomainException("La mesa debe indicar la materia.");
        }

        PlanMateriaId = planMateriaId;
        Asignar(datos);
        if (Estado == EstadoMesa.Publicada)
        {
            ValidarQueNoPaso(ahora, "Una mesa publicada no puede pasar a una fecha que ya pasó.");
        }

        UpdatedAt = ahora;
    }

    public void Publicar(DateTimeOffset ahora)
    {
        if (Estado != EstadoMesa.Borrador)
        {
            throw new DomainException(Estado == EstadoMesa.Publicada
                ? "La mesa ya está publicada."
                : "Una mesa cancelada no se puede publicar.");
        }

        ValidarQueNoPaso(ahora, "No se puede publicar una mesa cuya fecha ya pasó.");
        Estado = EstadoMesa.Publicada;
        PublicadaAt = ahora;
        UpdatedAt = ahora;
    }

    /// <summary>Solo se cancela una mesa publicada: un borrador simplemente se elimina.</summary>
    public void Cancelar(string motivo, DateTimeOffset ahora)
    {
        if (Estado != EstadoMesa.Publicada)
        {
            throw new DomainException(Estado == EstadoMesa.Cancelada
                ? "La mesa ya está cancelada."
                : "Un borrador no se cancela: eliminalo.");
        }

        MotivoCancelacion = Texto.Requerido(motivo, "motivo de la cancelación", 300);
        Estado = EstadoMesa.Cancelada;
        CanceladaAt = ahora;
        UpdatedAt = ahora;
    }

    public void ValidarQueSePuedeEliminar()
    {
        if (Estado != EstadoMesa.Borrador)
        {
            throw new DomainException("Solo se eliminan borradores. Una mesa publicada se cancela, así queda registro.");
        }
    }

    /// <summary>Estado de la inscripción para el día de hoy en el instituto. Una mesa cancelada no admite inscripción.</summary>
    public EstadoInscripcion EstadoInscripcionAl(DateOnly hoy)
    {
        if (Estado == EstadoMesa.Cancelada)
        {
            return EstadoInscripcion.Cerrada;
        }

        if (InscripcionDesde is null || InscripcionHasta is null)
        {
            return EstadoInscripcion.SinFechas;
        }

        if (hoy < InscripcionDesde)
        {
            return EstadoInscripcion.Proxima;
        }

        return hoy <= InscripcionHasta ? EstadoInscripcion.Abierta : EstadoInscripcion.Cerrada;
    }

    private void Asignar(DatosMesa datos)
    {
        Turno = Texto.Requerido(datos.Turno, "turno", 60);
        Llamado = datos.Llamado is >= 1 and <= MaximoLlamado
            ? datos.Llamado
            : throw new DomainException($"El llamado debe estar entre 1 y {MaximoLlamado}.");

        Fecha = datos.Fecha;
        Hora = new TimeOnly(datos.Hora.Hour, datos.Hora.Minute);
        Aula = Texto.Opcional(datos.Aula, "aula", 50);
        Observaciones = Texto.Opcional(datos.Observaciones, "texto de las observaciones", 500);
        _tribunal = ValidarTribunal(datos.Tribunal);
        (InscripcionDesde, InscripcionHasta) = ValidarInscripcion(datos.InscripcionDesde, datos.InscripcionHasta, datos.Fecha);
    }

    private void ValidarQueNoPaso(DateTimeOffset ahora, string mensaje)
    {
        if (Inicio <= ahora)
        {
            throw new DomainException(mensaje);
        }
    }

    private static List<string> ValidarTribunal(IEnumerable<string>? integrantes)
    {
        var limpios = (integrantes ?? [])
            .Where(integrante => !string.IsNullOrWhiteSpace(integrante))
            .Select(integrante => Texto.Requerido(integrante, "nombre del integrante del tribunal", 120))
            .ToList();

        if (limpios.Count == 0)
        {
            throw new DomainException("El tribunal debe tener al menos un integrante.");
        }

        if (limpios.Count > MaximoIntegrantesTribunal)
        {
            throw new DomainException($"El tribunal no puede tener más de {MaximoIntegrantesTribunal} integrantes.");
        }

        if (limpios.Distinct(StringComparer.OrdinalIgnoreCase).Count() != limpios.Count)
        {
            throw new DomainException("Un integrante figura dos veces en el tribunal.");
        }

        return limpios;
    }

    private static (DateOnly?, DateOnly?) ValidarInscripcion(DateOnly? desde, DateOnly? hasta, DateOnly fecha)
    {
        if (desde is null && hasta is null)
        {
            return (null, null);
        }

        if (desde is null || hasta is null)
        {
            throw new DomainException("Indicá el inicio y el cierre de la inscripción, o ninguno de los dos.");
        }

        if (desde > hasta)
        {
            throw new DomainException("La inscripción no puede cerrar antes de abrir.");
        }

        if (hasta >= fecha)
        {
            throw new DomainException("La inscripción tiene que cerrar antes del día del examen.");
        }

        return (desde, hasta);
    }
}

/// <summary>Datos editables de una mesa, en hora del instituto.</summary>
public sealed record DatosMesa(
    string Turno,
    int Llamado,
    DateOnly Fecha,
    TimeOnly Hora,
    string? Aula,
    IReadOnlyList<string> Tribunal,
    DateOnly? InscripcionDesde,
    DateOnly? InscripcionHasta,
    string? Observaciones);
