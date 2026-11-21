using Astrea.Application.Contracts.Results;
using Astrea.Domain.Exams;

namespace Astrea.Application.Features.Exams;

/// <summary>
/// Mesa de examen tal como la ven estudiantes y administración. <see cref="Fecha"/> y <see cref="Hora"/>
/// están en hora del instituto; <see cref="Inicio"/> es el mismo momento con su desfase (ej. -03:00).
/// </summary>
public sealed record MesaExamenDto(
    Guid Id,
    EstadoMesa Estado,
    string Turno,
    int Llamado,
    DateOnly Fecha,
    TimeOnly Hora,
    DateTimeOffset Inicio,
    string? Aula,
    IReadOnlyList<string> Tribunal,
    DateOnly? InscripcionDesde,
    DateOnly? InscripcionHasta,
    EstadoInscripcion EstadoInscripcion,
    string? Observaciones,
    string? MotivoCancelacion,
    Guid PlanId,
    Guid CarreraId,
    string CarreraCodigo,
    string CarreraNombre,
    Guid MateriaId,
    string MateriaCodigo,
    string MateriaNombre,
    int AnioMateria);

/// <summary>
/// Filtros permitidos para buscar mesas. Las fechas son días del instituto e incluyen ambos extremos.
/// <see cref="Texto"/> busca en el nombre de la materia y en el tribunal, sin distinguir tildes ni mayúsculas.
/// </summary>
public sealed record FiltroMesas(
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    Guid? CarreraId = null,
    Guid? MateriaId = null,
    string? Texto = null,
    EstadoMesa? Estado = null,
    IReadOnlyList<Guid>? Carreras = null);

/// <summary>Alta o corrección de una mesa. La materia se indica por plan y materia, como en el resto de la API.</summary>
public sealed record GuardarMesaCommand(
    Guid PlanId,
    Guid MateriaId,
    string Turno,
    int Llamado,
    DateOnly Fecha,
    TimeOnly Hora,
    string? Aula,
    IReadOnlyList<string>? Tribunal,
    DateOnly? InscripcionDesde,
    DateOnly? InscripcionHasta,
    string? Observaciones);

public sealed record CancelarMesaCommand(string Motivo);

public static class ErroresMesas
{
    public const int LargoMaximoTexto = 100;

    public static readonly Error MesaNoEncontrada =
        Error.NoEncontrado("mesa_no_encontrada", "La mesa de examen no existe.");

    public static readonly Error MateriaNoEncontrada =
        Error.NoEncontrado("materia_no_encontrada", "La materia no existe en ese plan de estudios.");

    public static readonly Error RangoDeFechasInvalido =
        Error.Validacion("rango_fechas_invalido", "La fecha \"desde\" no puede ser posterior a la fecha \"hasta\".");

    public static readonly Error TextoDemasiadoLargo =
        Error.Validacion("texto_demasiado_largo", $"La búsqueda no puede superar los {LargoMaximoTexto} caracteres.");

    public static readonly Error HorarioOcupado =
        Error.Conflicto("mesa_duplicada", "Ya hay una mesa vigente de esa materia en el mismo día y horario.");
}
