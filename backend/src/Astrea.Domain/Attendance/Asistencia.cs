using Astrea.Domain.Common;
using Astrea.Domain.Exams;

namespace Astrea.Domain.Attendance;

/// <summary>Lo que el profesor confirma de cada estudiante en un relevamiento.</summary>
public enum ValorAsistencia
{
    AsisteRegularmente = 1,
    AsisteIrregularmente = 2,
    NoAsiste = 3,
}

/// <summary>Secretaría asigna a un profesor (usuario con rol Docente) una materia de un plan. Solo ve a los estudiantes de esas materias.</summary>
public sealed class AsignacionDocente : Entity
{
    private AsignacionDocente()
    {
    }

    private AsignacionDocente(Guid id, Guid docenteId, Guid planMateriaId, Guid asignadaPor, DateTimeOffset ahora)
        : base(id)
    {
        DocenteId = docenteId;
        PlanMateriaId = planMateriaId;
        AsignadaPor = asignadaPor;
        FechaAsignacion = ahora;
    }

    public Guid DocenteId { get; private set; }

    public Guid PlanMateriaId { get; private set; }

    public Guid AsignadaPor { get; private set; }

    public DateTimeOffset FechaAsignacion { get; private set; }

    public static AsignacionDocente Crear(Guid id, Guid docenteId, Guid planMateriaId, Guid asignadaPor, DateTimeOffset ahora)
    {
        if (docenteId == Guid.Empty || planMateriaId == Guid.Empty)
        {
            throw new DomainException("La asignación debe indicar el docente y la materia.");
        }

        return new AsignacionDocente(id, docenteId, planMateriaId, asignadaPor, ahora);
    }
}

/// <summary>
/// El estudiante declara que cursa una materia en un ciclo lectivo. Con eso su profesor lo ve en la lista
/// para confirmar asistencia. Se puede dar de baja mientras no tenga confirmaciones.
/// </summary>
public sealed class InscripcionCursada : Entity
{
    public const int PrimerCiclo = 2020;

    private InscripcionCursada()
    {
    }

    private InscripcionCursada(Guid id, Guid estudianteId, Guid planMateriaId, int cicloLectivo, DateTimeOffset ahora)
        : base(id)
    {
        EstudianteId = estudianteId;
        PlanMateriaId = planMateriaId;
        CicloLectivo = cicloLectivo;
        FechaInscripcion = ahora;
    }

    public Guid EstudianteId { get; private set; }

    public Guid PlanMateriaId { get; private set; }

    public int CicloLectivo { get; private set; }

    public DateTimeOffset FechaInscripcion { get; private set; }

    /// <summary>El ciclo lectivo vigente: el año calendario del instituto.</summary>
    public static int CicloActual(DateTimeOffset ahora) => ZonaHorariaInstitucional.Hoy(ahora).Year;

    public static InscripcionCursada Crear(Guid id, Guid estudianteId, Guid planMateriaId, DateTimeOffset ahora)
    {
        if (estudianteId == Guid.Empty || planMateriaId == Guid.Empty)
        {
            throw new DomainException("La inscripción debe indicar el estudiante y la materia.");
        }

        return new InscripcionCursada(id, estudianteId, planMateriaId, CicloActual(ahora), ahora);
    }
}

/// <summary>
/// Un pedido de la dirección a los profesores: "confirmen quién asiste" (por ejemplo, antes del receso de julio, para
/// la beca Progresar). Mientras está abierto, cada profesor confirma a los estudiantes inscriptos en sus materias.
/// No registra quién cobra una beca: releva la asistencia de todos por igual.
/// </summary>
public sealed class RelevamientoAsistencia : Entity
{
    public const int LargoMaximoTitulo = 120;

    private RelevamientoAsistencia()
    {
    }

    private RelevamientoAsistencia(Guid id)
        : base(id)
    {
    }

    public string Titulo { get; private set; } = null!;

    public int CicloLectivo { get; private set; }

    /// <summary>Último día (del instituto) en que los profesores pueden confirmar.</summary>
    public DateOnly FechaCierre { get; private set; }

    public Guid CreadoPor { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    /// <summary>Si la dirección lo cerró antes de la fecha de cierre.</summary>
    public DateTimeOffset? CerradoEn { get; private set; }

    public static RelevamientoAsistencia Abrir(Guid id, string titulo, DateOnly fechaCierre, Guid creadoPor, DateTimeOffset ahora)
    {
        var hoy = ZonaHorariaInstitucional.Hoy(ahora);
        if (fechaCierre < hoy)
        {
            throw new DomainException("La fecha de cierre no puede ser anterior a hoy.");
        }

        if (fechaCierre > hoy.AddDays(120))
        {
            throw new DomainException("El relevamiento puede durar como máximo 120 días.");
        }

        return new RelevamientoAsistencia(id)
        {
            Titulo = Texto.Requerido(titulo, "título", LargoMaximoTitulo),
            CicloLectivo = hoy.Year,
            FechaCierre = fechaCierre,
            CreadoPor = creadoPor,
            CreadoEn = ahora,
        };
    }

    public bool EstaAbierto(DateTimeOffset ahora) => CerradoEn is null && ZonaHorariaInstitucional.Hoy(ahora) <= FechaCierre;

    public void Cerrar(DateTimeOffset ahora)
    {
        if (!EstaAbierto(ahora))
        {
            throw new DomainException("El relevamiento ya está cerrado.");
        }

        CerradoEn = ahora;
    }
}

/// <summary>
/// La confirmación de un profesor sobre un estudiante en una materia, dentro de un relevamiento. Se puede corregir
/// mientras el relevamiento esté abierto; queda registrado quién confirmó y cuándo.
/// </summary>
public sealed class ConfirmacionAsistencia : Entity
{
    public const int LargoMaximoObservacion = 200;

    private ConfirmacionAsistencia()
    {
    }

    private ConfirmacionAsistencia(Guid id, Guid relevamientoId, Guid planMateriaId, Guid estudianteId)
        : base(id)
    {
        RelevamientoId = relevamientoId;
        PlanMateriaId = planMateriaId;
        EstudianteId = estudianteId;
    }

    public Guid RelevamientoId { get; private set; }

    public Guid PlanMateriaId { get; private set; }

    public Guid EstudianteId { get; private set; }

    public ValorAsistencia Valor { get; private set; }

    public string? Observacion { get; private set; }

    public Guid DocenteId { get; private set; }

    public DateTimeOffset ConfirmadaEn { get; private set; }

    public static ConfirmacionAsistencia Crear(
        Guid id,
        RelevamientoAsistencia relevamiento,
        Guid planMateriaId,
        Guid estudianteId,
        Guid docenteId,
        ValorAsistencia valor,
        string? observacion,
        DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(relevamiento);
        var confirmacion = new ConfirmacionAsistencia(id, relevamiento.Id, planMateriaId, estudianteId);
        confirmacion.Actualizar(relevamiento, docenteId, valor, observacion, ahora);
        return confirmacion;
    }

    public void Actualizar(RelevamientoAsistencia relevamiento, Guid docenteId, ValorAsistencia valor, string? observacion, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(relevamiento);
        if (relevamiento.Id != RelevamientoId)
        {
            throw new DomainException("La confirmación es de otro relevamiento.");
        }

        if (!relevamiento.EstaAbierto(ahora))
        {
            throw new DomainException("El relevamiento está cerrado: ya no se pueden cambiar las confirmaciones.");
        }

        if (!Enum.IsDefined(valor))
        {
            throw new DomainException("Indicá si el estudiante asiste regularmente, de forma irregular o no asiste.");
        }

        Valor = valor;
        Observacion = Texto.Opcional(observacion, "observación", LargoMaximoObservacion);
        DocenteId = docenteId;
        ConfirmadaEn = ahora;
    }
}
