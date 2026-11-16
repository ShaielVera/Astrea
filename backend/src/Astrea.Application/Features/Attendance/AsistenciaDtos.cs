using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Contracts.Results;
using Astrea.Domain.Attendance;

namespace Astrea.Application.Features.Attendance;

public sealed record MateriaCursadaDto(Guid PlanMateriaId, string Materia, int Anio, Guid CarreraId, string Carrera);

public sealed record UsuarioEncontradoDto(Guid Id, string Email, string Nombre, string? Dni, bool EsDocente);

public sealed record AsignacionDto(Guid Id, Guid PlanMateriaId, string Materia, int Anio, string Carrera);

public sealed record DocenteDto(Guid Id, string Nombre, string Email, IReadOnlyList<AsignacionDto> Materias);

public sealed record EstudianteDeMateriaDto(Guid Id, string Nombre, string Email);

public sealed record RelevamientoDto(Guid Id, string Titulo, int CicloLectivo, DateOnly FechaCierre, bool Abierto, DateTimeOffset CreadoEn)
{
    public static RelevamientoDto Desde(RelevamientoAsistencia relevamiento, DateTimeOffset ahora) =>
        new(relevamiento.Id, relevamiento.Titulo, relevamiento.CicloLectivo, relevamiento.FechaCierre, relevamiento.EstaAbierto(ahora), relevamiento.CreadoEn);
}

public sealed record EstudianteAConfirmarDto(Guid Id, string Nombre, string Email, ValorAsistencia? Valor, string? Observacion);

public sealed record MateriaAConfirmarDto(Guid PlanMateriaId, string Materia, int Anio, string Carrera, IReadOnlyList<EstudianteAConfirmarDto> Estudiantes);

public sealed record PanelDocenteDto(RelevamientoDto Relevamiento, IReadOnlyList<MateriaAConfirmarDto> Materias);

public sealed record ConfirmacionItem(Guid EstudianteId, ValorAsistencia Valor, string? Observacion);

public sealed record ConfirmarAsistenciaCommand(IReadOnlyList<ConfirmacionItem>? Confirmaciones);

public sealed record AbrirRelevamientoCommand(string? Titulo, DateOnly FechaCierre);

public sealed record MateriaInformeDto(string Materia, string Carrera, int Anio, ValorAsistencia? Valor, string? Observacion, string? Docente, DateTimeOffset? ConfirmadaEn);

public sealed record EstudianteInformeDto(
    Guid Id,
    string Nombre,
    string Email,
    string? Dni,
    IReadOnlyList<MateriaInformeDto> Materias,
    int Regular,
    int Irregular,
    int NoAsiste,
    int SinConfirmar);

/// <summary>Informe para la dirección. <see cref="DnisSinDatos"/>: DNI buscados que no corresponden a ningún estudiante inscripto en materias del ciclo.</summary>
public sealed record InformeAsistenciaDto(RelevamientoDto Relevamiento, PagedResult<EstudianteInformeDto> Estudiantes, IReadOnlyList<string> DnisSinDatos);

public sealed record MateriaAvanceDto(Guid PlanMateriaId, string Materia, int Anio, string Carrera, int Inscriptos, int Confirmados, IReadOnlyList<string> Docentes);

public sealed record AvanceRelevamientoDto(RelevamientoDto Relevamiento, int Inscripciones, int Confirmadas, IReadOnlyList<MateriaAvanceDto> Materias);

public sealed record MiConfirmacionDto(Guid RelevamientoId, string Relevamiento, DateOnly FechaCierre, string Materia, ValorAsistencia Valor, string? Observacion, DateTimeOffset ConfirmadaEn);

public sealed record MiAsistenciaDto(int CicloLectivo, IReadOnlyList<MateriaCursadaDto> Materias, IReadOnlyList<MiConfirmacionDto> Confirmaciones);

public sealed record FilaInscripcion(Guid EstudianteId, string Nombre, string Email, string? Dni, MateriaCursadaDto Materia);

public sealed record FilaConfirmacion(Guid EstudianteId, Guid PlanMateriaId, ValorAsistencia Valor, string? Observacion, string Docente, DateTimeOffset ConfirmadaEn);

public sealed record DatosInforme(IReadOnlyList<FilaInscripcion> Inscripciones, IReadOnlyList<FilaConfirmacion> Confirmaciones, IReadOnlyDictionary<Guid, IReadOnlyList<string>> DocentesPorMateria);

public static class ErroresAsistencia
{
    public static readonly Error UsuarioNoEncontrado = Error.NoEncontrado("usuario_no_encontrado", "El usuario no existe o está desactivado.");

    public static readonly Error NoEsDocente = Error.Validacion("no_es_docente", "El usuario no tiene el rol de docente.");

    public static readonly Error MateriaNoEncontrada = Error.NoEncontrado("materia_no_encontrada", "La materia no existe en ningún plan.");

    public static readonly Error AsignacionNoEncontrada = Error.NoEncontrado("asignacion_no_encontrada", "La asignación no existe.");

    public static readonly Error AsignacionRepetida = Error.Conflicto("asignacion_repetida", "El docente ya tiene asignada esa materia.");

    public static readonly Error InscripcionRepetida = Error.Conflicto("inscripcion_repetida", "Ya estás inscripto en esa materia este año.");

    public static readonly Error InscripcionNoEncontrada = Error.NoEncontrado("inscripcion_no_encontrada", "No estás inscripto en esa materia este año.");

    public static readonly Error BajaConConfirmaciones = Error.Conflicto(
        "baja_con_confirmaciones",
        "Tu profesor ya confirmó tu asistencia en esa materia: para darte de baja hablá con Secretaría.");

    public static readonly Error RelevamientoNoEncontrado = Error.NoEncontrado("relevamiento_no_encontrado", "El relevamiento no existe.");

    public static readonly Error RelevamientoCerrado = Error.Conflicto("relevamiento_cerrado", "El relevamiento está cerrado: ya no se pueden cambiar las confirmaciones.");

    public static readonly Error MateriaNoAsignada = Error.NoEncontrado("materia_no_asignada", "No tenés asignada esa materia.");

    public static readonly Error SinConfirmaciones = Error.Validacion("sin_confirmaciones", "No hay confirmaciones para guardar.");

    public static readonly Error EstudianteNoInscripto = Error.Validacion("estudiante_no_inscripto", "Hay estudiantes que no están inscriptos en la materia.");

    public static readonly Error BusquedaCorta = Error.Validacion("busqueda_corta", "Escribí al menos 3 caracteres para buscar.");

    public static readonly Error DemasiadosDnis = Error.Validacion("demasiados_dnis", "Se pueden buscar hasta 500 DNI por vez.");
}
