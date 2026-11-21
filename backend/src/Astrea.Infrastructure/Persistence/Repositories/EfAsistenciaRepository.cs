using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Features.Attendance;
using Astrea.Domain.Attendance;
using Astrea.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Infrastructure.Persistence.Repositories;

internal sealed class EfAsistenciaRepository(AppDbContext db) : IAsistenciaRepository
{
    public async Task<MateriaCursadaDto?> ObtenerMateriaDelPlanAsync(Guid planMateriaId, CancellationToken cancellationToken) =>
        (await MateriasDelPlan().Where(materia => materia.PlanMateriaId == planMateriaId).ToListAsync(cancellationToken)).SingleOrDefault()?.ADto();

    public async Task<IReadOnlyList<UsuarioEncontradoDto>> BuscarUsuariosAsync(string texto, int maximo, CancellationToken cancellationToken)
    {
        var patron = $"%{texto.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";
        var usuarios = await db.Usuarios.AsNoTracking()
            .Where(usuario => usuario.Activo
                && (EF.Functions.ILike(usuario.Email, patron)
                    || (usuario.Apellido != null && EF.Functions.ILike(usuario.Apellido, patron))
                    || (usuario.Nombre != null && EF.Functions.ILike(usuario.Nombre, patron))
                    || (usuario.Dni != null && EF.Functions.ILike(usuario.Dni, patron))))
            .OrderBy(usuario => usuario.Apellido ?? usuario.Email)
            .ThenBy(usuario => usuario.Id)
            .Take(maximo)
            .ToListAsync(cancellationToken);
        return usuarios.Select(usuario => new UsuarioEncontradoDto(usuario.Id, usuario.Email, usuario.NombreParaMostrar, usuario.Dni, usuario.TieneRol(Rol.Docente))).ToList();
    }

    public async Task<IReadOnlyList<DocenteDto>> ListarDocentesAsync(CancellationToken cancellationToken)
    {
        var docentes = await db.Usuarios.AsNoTracking()
            .Where(usuario => usuario.Activo && db.UsuarioRoles.Any(asignacion => asignacion.UsuarioId == usuario.Id && asignacion.RolId == Rol.Docente.Id))
            .ToListAsync(cancellationToken);
        var ids = docentes.Select(docente => docente.Id).ToList();
        var asignaciones = await (
                from asignacion in db.AsignacionesDocentes.AsNoTracking()
                join materia in MateriasDelPlan() on asignacion.PlanMateriaId equals materia.PlanMateriaId
                where ids.Contains(asignacion.DocenteId)
                select new { asignacion.Id, asignacion.DocenteId, Materia = materia })
            .ToListAsync(cancellationToken);

        return docentes
            .Select(docente => new DocenteDto(
                docente.Id,
                docente.NombreParaMostrar,
                docente.Email,
                asignaciones.Where(asignacion => asignacion.DocenteId == docente.Id)
                    .OrderBy(asignacion => asignacion.Materia.Carrera, StringComparer.CurrentCulture)
                    .ThenBy(asignacion => asignacion.Materia.Anio)
                    .ThenBy(asignacion => asignacion.Materia.Materia, StringComparer.CurrentCulture)
                    .Select(asignacion => new AsignacionDto(asignacion.Id, asignacion.Materia.PlanMateriaId, asignacion.Materia.Materia, asignacion.Materia.Anio, asignacion.Materia.Carrera))
                    .ToList()))
            .OrderBy(docente => docente.Nombre, StringComparer.CurrentCulture)
            .ToList();
    }

    public Task<AsignacionDocente?> ObtenerAsignacionAsync(Guid asignacionId, CancellationToken cancellationToken) =>
        db.AsignacionesDocentes.SingleOrDefaultAsync(asignacion => asignacion.Id == asignacionId, cancellationToken);

    public Task<AsignacionDocente?> ObtenerAsignacionAsync(Guid docenteId, Guid planMateriaId, CancellationToken cancellationToken) =>
        db.AsignacionesDocentes.SingleOrDefaultAsync(asignacion => asignacion.DocenteId == docenteId && asignacion.PlanMateriaId == planMateriaId, cancellationToken);

    public Task<bool> ExisteAsignacionAsync(Guid docenteId, Guid planMateriaId, CancellationToken cancellationToken) =>
        db.AsignacionesDocentes.AnyAsync(asignacion => asignacion.DocenteId == docenteId && asignacion.PlanMateriaId == planMateriaId, cancellationToken);

    public async Task<IReadOnlyList<MateriaCursadaDto>> MateriasDelDocenteAsync(Guid docenteId, CancellationToken cancellationToken) =>
        Ordenar(await (
                from asignacion in db.AsignacionesDocentes.AsNoTracking()
                join materia in MateriasDelPlan() on asignacion.PlanMateriaId equals materia.PlanMateriaId
                where asignacion.DocenteId == docenteId
                select materia)
            .ToListAsync(cancellationToken));

    public void Agregar(AsignacionDocente asignacion) => db.AsignacionesDocentes.Add(asignacion);

    public void Eliminar(AsignacionDocente asignacion) => db.AsignacionesDocentes.Remove(asignacion);

    public Task<int> EliminarAsignacionesDeAsync(Guid docenteId, CancellationToken cancellationToken) =>
        db.AsignacionesDocentes.Where(asignacion => asignacion.DocenteId == docenteId).ExecuteDeleteAsync(cancellationToken);

    public Task<InscripcionCursada?> ObtenerInscripcionAsync(Guid estudianteId, Guid planMateriaId, int cicloLectivo, CancellationToken cancellationToken) =>
        db.InscripcionesCursada.SingleOrDefaultAsync(
            inscripcion => inscripcion.EstudianteId == estudianteId && inscripcion.PlanMateriaId == planMateriaId && inscripcion.CicloLectivo == cicloLectivo,
            cancellationToken);

    public async Task<IReadOnlyList<MateriaCursadaDto>> MateriasDelEstudianteAsync(Guid estudianteId, int cicloLectivo, CancellationToken cancellationToken) =>
        Ordenar(await (
                from inscripcion in db.InscripcionesCursada.AsNoTracking()
                join materia in MateriasDelPlan() on inscripcion.PlanMateriaId equals materia.PlanMateriaId
                where inscripcion.EstudianteId == estudianteId && inscripcion.CicloLectivo == cicloLectivo
                select materia)
            .ToListAsync(cancellationToken));

    public void Agregar(InscripcionCursada inscripcion) => db.InscripcionesCursada.Add(inscripcion);

    public void Eliminar(InscripcionCursada inscripcion) => db.InscripcionesCursada.Remove(inscripcion);

    public Task<bool> TieneConfirmacionesAsync(Guid estudianteId, Guid planMateriaId, int cicloLectivo, CancellationToken cancellationToken) =>
        (from confirmacion in db.ConfirmacionesAsistencia
         join relevamiento in db.RelevamientosAsistencia on confirmacion.RelevamientoId equals relevamiento.Id
         where confirmacion.EstudianteId == estudianteId && confirmacion.PlanMateriaId == planMateriaId && relevamiento.CicloLectivo == cicloLectivo
         select confirmacion.Id).AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<EstudianteDeMateriaDto>> EstudiantesDeMateriaAsync(Guid planMateriaId, int cicloLectivo, CancellationToken cancellationToken)
    {
        var estudiantes = await (
                from inscripcion in db.InscripcionesCursada.AsNoTracking()
                join usuario in db.Usuarios.AsNoTracking() on inscripcion.EstudianteId equals usuario.Id
                where inscripcion.PlanMateriaId == planMateriaId && inscripcion.CicloLectivo == cicloLectivo && usuario.Activo
                select usuario)
            .ToListAsync(cancellationToken);
        return estudiantes
            .Select(usuario => new EstudianteDeMateriaDto(usuario.Id, usuario.NombreParaMostrar, usuario.Email))
            .OrderBy(estudiante => estudiante.Nombre, StringComparer.CurrentCulture)
            .ToList();
    }

    public Task<RelevamientoAsistencia?> ObtenerRelevamientoAsync(Guid relevamientoId, CancellationToken cancellationToken) =>
        db.RelevamientosAsistencia.SingleOrDefaultAsync(relevamiento => relevamiento.Id == relevamientoId, cancellationToken);

    public async Task<IReadOnlyList<RelevamientoAsistencia>> ListarRelevamientosAsync(CancellationToken cancellationToken) =>
        await db.RelevamientosAsistencia.AsNoTracking().OrderByDescending(relevamiento => relevamiento.CreadoEn).ThenByDescending(relevamiento => relevamiento.Id).ToListAsync(cancellationToken);

    public void Agregar(RelevamientoAsistencia relevamiento) => db.RelevamientosAsistencia.Add(relevamiento);

    public async Task<IReadOnlyList<ConfirmacionAsistencia>> ConfirmacionesDeMateriaAsync(Guid relevamientoId, Guid planMateriaId, CancellationToken cancellationToken) =>
        await db.ConfirmacionesAsistencia
            .Where(confirmacion => confirmacion.RelevamientoId == relevamientoId && confirmacion.PlanMateriaId == planMateriaId)
            .ToListAsync(cancellationToken);

    public void Agregar(ConfirmacionAsistencia confirmacion) => db.ConfirmacionesAsistencia.Add(confirmacion);

    public async Task<DatosInforme> DatosDelInformeAsync(RelevamientoAsistencia relevamiento, CancellationToken cancellationToken)
    {
        var inscripciones = await (
                from inscripcion in db.InscripcionesCursada.AsNoTracking()
                join usuario in db.Usuarios.AsNoTracking() on inscripcion.EstudianteId equals usuario.Id
                join materia in MateriasDelPlan() on inscripcion.PlanMateriaId equals materia.PlanMateriaId
                where inscripcion.CicloLectivo == relevamiento.CicloLectivo && usuario.Activo
                select new { Usuario = usuario, Materia = materia })
            .ToListAsync(cancellationToken);

        var confirmaciones = await (
                from confirmacion in db.ConfirmacionesAsistencia.AsNoTracking()
                join docente in db.Usuarios.AsNoTracking() on confirmacion.DocenteId equals docente.Id
                where confirmacion.RelevamientoId == relevamiento.Id
                select new { Confirmacion = confirmacion, Docente = docente })
            .ToListAsync(cancellationToken);

        var docentes = await (
                from asignacion in db.AsignacionesDocentes.AsNoTracking()
                join usuario in db.Usuarios.AsNoTracking() on asignacion.DocenteId equals usuario.Id
                select new { asignacion.PlanMateriaId, Usuario = usuario })
            .ToListAsync(cancellationToken);

        return new DatosInforme(
            inscripciones.Select(fila => new FilaInscripcion(fila.Usuario.Id, fila.Usuario.NombreParaMostrar, fila.Usuario.Email, fila.Usuario.Dni, fila.Materia.ADto())).ToList(),
            confirmaciones.Select(fila => new FilaConfirmacion(
                fila.Confirmacion.EstudianteId,
                fila.Confirmacion.PlanMateriaId,
                fila.Confirmacion.Valor,
                fila.Confirmacion.Observacion,
                fila.Docente.NombreParaMostrar,
                fila.Confirmacion.ConfirmadaEn)).ToList(),
            docentes.GroupBy(fila => fila.PlanMateriaId)
                .ToDictionary(grupo => grupo.Key, grupo => (IReadOnlyList<string>)grupo.Select(fila => fila.Usuario.NombreParaMostrar).Order(StringComparer.CurrentCulture).ToList()));
    }

    public async Task<IReadOnlyList<MiConfirmacionDto>> ConfirmacionesDelEstudianteAsync(Guid estudianteId, int cicloLectivo, CancellationToken cancellationToken) =>
        (await (
                from confirmacion in db.ConfirmacionesAsistencia.AsNoTracking()
                join relevamiento in db.RelevamientosAsistencia.AsNoTracking() on confirmacion.RelevamientoId equals relevamiento.Id
                join materia in MateriasDelPlan() on confirmacion.PlanMateriaId equals materia.PlanMateriaId
                where confirmacion.EstudianteId == estudianteId && relevamiento.CicloLectivo == cicloLectivo
                select new { Confirmacion = confirmacion, Relevamiento = relevamiento, materia.Materia })
            .ToListAsync(cancellationToken))
        .OrderByDescending(fila => fila.Relevamiento.CreadoEn)
        .ThenBy(fila => fila.Materia, StringComparer.CurrentCulture)
        .Select(fila => new MiConfirmacionDto(
            fila.Relevamiento.Id,
            fila.Relevamiento.Titulo,
            fila.Relevamiento.FechaCierre,
            fila.Materia,
            fila.Confirmacion.Valor,
            fila.Confirmacion.Observacion,
            fila.Confirmacion.ConfirmadaEn))
        .ToList();

    private IQueryable<FilaMateria> MateriasDelPlan() =>
        from planMateria in db.PlanMaterias.AsNoTracking()
        join materia in db.Materias.AsNoTracking() on planMateria.MateriaId equals materia.Id
        join plan in db.PlanesEstudio.AsNoTracking() on planMateria.PlanEstudioId equals plan.Id
        join carrera in db.Carreras.AsNoTracking() on plan.CarreraId equals carrera.Id
        select new FilaMateria { PlanMateriaId = planMateria.Id, Materia = materia.Nombre, Anio = planMateria.AnioEnPlan, CarreraId = carrera.Id, Carrera = carrera.Nombre };

    private static List<MateriaCursadaDto> Ordenar(IEnumerable<FilaMateria> materias) =>
        materias.Select(materia => materia.ADto()).OrderBy(materia => materia.Carrera, StringComparer.CurrentCulture)
            .ThenBy(materia => materia.Anio)
            .ThenBy(materia => materia.Materia, StringComparer.CurrentCulture)
            .ToList();

    private sealed class FilaMateria
    {
        public Guid PlanMateriaId { get; init; }

        public string Materia { get; init; } = null!;

        public int Anio { get; init; }

        public Guid CarreraId { get; init; }

        public string Carrera { get; init; } = null!;

        public MateriaCursadaDto ADto() => new(PlanMateriaId, Materia, Anio, CarreraId, Carrera);
    }
}
