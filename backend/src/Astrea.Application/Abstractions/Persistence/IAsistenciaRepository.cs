using Astrea.Application.Features.Attendance;
using Astrea.Domain.Attendance;

namespace Astrea.Application.Abstractions.Persistence;

/// <summary>Docentes y sus materias, inscripciones a cursada, relevamientos de asistencia y confirmaciones.</summary>
public interface IAsistenciaRepository
{
    /// <summary>La materia de un plan con su carrera, o null si no existe.</summary>
    Task<MateriaCursadaDto?> ObtenerMateriaDelPlanAsync(Guid planMateriaId, CancellationToken cancellationToken);

    /// <summary>Usuarios activos cuyo email, apellido, nombre o DNI contiene el texto (para que Secretaría elija a un docente).</summary>
    Task<IReadOnlyList<UsuarioEncontradoDto>> BuscarUsuariosAsync(string texto, int maximo, CancellationToken cancellationToken);

    /// <summary>Los usuarios con rol Docente, con sus materias asignadas.</summary>
    Task<IReadOnlyList<DocenteDto>> ListarDocentesAsync(CancellationToken cancellationToken);

    Task<AsignacionDocente?> ObtenerAsignacionAsync(Guid asignacionId, CancellationToken cancellationToken);

    Task<AsignacionDocente?> ObtenerAsignacionAsync(Guid docenteId, Guid planMateriaId, CancellationToken cancellationToken);

    Task<bool> ExisteAsignacionAsync(Guid docenteId, Guid planMateriaId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MateriaCursadaDto>> MateriasDelDocenteAsync(Guid docenteId, CancellationToken cancellationToken);

    void Agregar(AsignacionDocente asignacion);

    void Eliminar(AsignacionDocente asignacion);

    Task<int> EliminarAsignacionesDeAsync(Guid docenteId, CancellationToken cancellationToken);

    Task<InscripcionCursada?> ObtenerInscripcionAsync(Guid estudianteId, Guid planMateriaId, int cicloLectivo, CancellationToken cancellationToken);

    Task<IReadOnlyList<MateriaCursadaDto>> MateriasDelEstudianteAsync(Guid estudianteId, int cicloLectivo, CancellationToken cancellationToken);

    void Agregar(InscripcionCursada inscripcion);

    void Eliminar(InscripcionCursada inscripcion);

    /// <summary>Si algún profesor ya confirmó la asistencia del estudiante en esa materia durante el ciclo.</summary>
    Task<bool> TieneConfirmacionesAsync(Guid estudianteId, Guid planMateriaId, int cicloLectivo, CancellationToken cancellationToken);

    /// <summary>Los estudiantes inscriptos en la materia en el ciclo, ordenados por apellido.</summary>
    Task<IReadOnlyList<EstudianteDeMateriaDto>> EstudiantesDeMateriaAsync(Guid planMateriaId, int cicloLectivo, CancellationToken cancellationToken);

    Task<RelevamientoAsistencia?> ObtenerRelevamientoAsync(Guid relevamientoId, CancellationToken cancellationToken);

    /// <summary>Los relevamientos, el más reciente primero.</summary>
    Task<IReadOnlyList<RelevamientoAsistencia>> ListarRelevamientosAsync(CancellationToken cancellationToken);

    void Agregar(RelevamientoAsistencia relevamiento);

    Task<IReadOnlyList<ConfirmacionAsistencia>> ConfirmacionesDeMateriaAsync(Guid relevamientoId, Guid planMateriaId, CancellationToken cancellationToken);

    void Agregar(ConfirmacionAsistencia confirmacion);

    /// <summary>Todo lo necesario para el informe: inscripciones del ciclo, con estudiante y materia, y las confirmaciones del relevamiento.</summary>
    Task<DatosInforme> DatosDelInformeAsync(RelevamientoAsistencia relevamiento, CancellationToken cancellationToken);

    /// <summary>Lo que confirmaron los profesores sobre el estudiante en los relevamientos del ciclo.</summary>
    Task<IReadOnlyList<MiConfirmacionDto>> ConfirmacionesDelEstudianteAsync(Guid estudianteId, int cicloLectivo, CancellationToken cancellationToken);
}
