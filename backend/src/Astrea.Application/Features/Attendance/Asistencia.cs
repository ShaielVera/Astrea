using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Auth;
using Astrea.Domain.Attendance;
using Astrea.Domain.Users;

namespace Astrea.Application.Features.Attendance;

/// <summary>
/// Secretaría da el rol Docente a un usuario y le asigna las materias que dicta. Un docente solo ve a los estudiantes
/// de sus materias.
/// </summary>
public sealed class GestionDocentes(UsuarioActual usuarioActual, IUserRepository usuarios, IAsistenciaRepository asistencia, IUnitOfWork unitOfWork, IClock clock)
{
    public const int MaximoResultados = 20;

    public async Task<Result<IReadOnlyList<UsuarioEncontradoDto>>> BuscarUsuariosAsync(string? texto, CancellationToken cancellationToken)
    {
        texto = texto?.Trim() ?? string.Empty;
        if (texto.Length < 3)
        {
            return ErroresAsistencia.BusquedaCorta;
        }

        return Result<IReadOnlyList<UsuarioEncontradoDto>>.Exito(await asistencia.BuscarUsuariosAsync(texto[..Math.Min(texto.Length, 100)], MaximoResultados, cancellationToken));
    }

    public async Task<IReadOnlyList<DocenteDto>> ListarAsync(CancellationToken cancellationToken) =>
        await asistencia.ListarDocentesAsync(cancellationToken);

    public async Task<Result<bool>> HacerDocenteAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var admin = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!admin.EsExitoso)
        {
            return admin.Error!;
        }

        var usuario = await usuarios.ObtenerPorIdAsync(usuarioId, cancellationToken);
        if (usuario is not { Activo: true })
        {
            return ErroresAsistencia.UsuarioNoEncontrado;
        }

        usuario.AsignarRol(Rol.Docente, admin.Valor.Id, clock.UtcNow);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return true;
    }

    /// <summary>Le quita el rol y todas sus materias. Las confirmaciones que ya hizo se conservan.</summary>
    public async Task<Result<bool>> QuitarDocenteAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var usuario = await usuarios.ObtenerPorIdAsync(usuarioId, cancellationToken);
        if (usuario is null)
        {
            return ErroresAsistencia.UsuarioNoEncontrado;
        }

        usuario.QuitarRol(Rol.Docente, clock.UtcNow);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        await asistencia.EliminarAsignacionesDeAsync(usuarioId, cancellationToken);
        return true;
    }

    public async Task<Result<AsignacionDto>> AsignarMateriaAsync(Guid docenteId, Guid planMateriaId, CancellationToken cancellationToken)
    {
        var admin = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!admin.EsExitoso)
        {
            return admin.Error!;
        }

        var docente = await usuarios.ObtenerPorIdAsync(docenteId, cancellationToken);
        if (docente is not { Activo: true })
        {
            return ErroresAsistencia.UsuarioNoEncontrado;
        }

        if (!docente.TieneRol(Rol.Docente))
        {
            return ErroresAsistencia.NoEsDocente;
        }

        var materia = await asistencia.ObtenerMateriaDelPlanAsync(planMateriaId, cancellationToken);
        if (materia is null)
        {
            return ErroresAsistencia.MateriaNoEncontrada;
        }

        if (await asistencia.ExisteAsignacionAsync(docenteId, planMateriaId, cancellationToken))
        {
            return ErroresAsistencia.AsignacionRepetida;
        }

        var asignacion = AsignacionDocente.Crear(Guid.CreateVersion7(), docenteId, planMateriaId, admin.Valor.Id, clock.UtcNow);
        asistencia.Agregar(asignacion);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return new AsignacionDto(asignacion.Id, materia.PlanMateriaId, materia.Materia, materia.Anio, materia.Carrera);
    }

    public async Task<Result<bool>> QuitarAsignacionAsync(Guid asignacionId, CancellationToken cancellationToken)
    {
        var asignacion = await asistencia.ObtenerAsignacionAsync(asignacionId, cancellationToken);
        if (asignacion is null)
        {
            return ErroresAsistencia.AsignacionNoEncontrada;
        }

        asistencia.Eliminar(asignacion);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return true;
    }
}

/// <summary>
/// El estudiante declara qué materias cursa este año; con eso aparece en la lista de su profesor. Ve lo que los profesores
/// confirmaron sobre él (transparencia: si no está de acuerdo, lo habla con Secretaría antes de que se use el dato).
/// </summary>
public sealed class CursadaEstudiante(UsuarioActual usuarioActual, IAsistenciaRepository asistencia, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<Result<MiAsistenciaDto>> MiAsistenciaAsync(CancellationToken cancellationToken)
    {
        var estudiante = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!estudiante.EsExitoso)
        {
            return estudiante.Error!;
        }

        var ciclo = InscripcionCursada.CicloActual(clock.UtcNow);
        return new MiAsistenciaDto(
            ciclo,
            await asistencia.MateriasDelEstudianteAsync(estudiante.Valor.Id, ciclo, cancellationToken),
            await asistencia.ConfirmacionesDelEstudianteAsync(estudiante.Valor.Id, ciclo, cancellationToken));
    }

    public async Task<Result<MateriaCursadaDto>> InscribirseAsync(Guid planMateriaId, CancellationToken cancellationToken)
    {
        var estudiante = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!estudiante.EsExitoso)
        {
            return estudiante.Error!;
        }

        var materia = await asistencia.ObtenerMateriaDelPlanAsync(planMateriaId, cancellationToken);
        if (materia is null)
        {
            return ErroresAsistencia.MateriaNoEncontrada;
        }

        var ahora = clock.UtcNow;
        if (await asistencia.ObtenerInscripcionAsync(estudiante.Valor.Id, planMateriaId, InscripcionCursada.CicloActual(ahora), cancellationToken) is not null)
        {
            return ErroresAsistencia.InscripcionRepetida;
        }

        asistencia.Agregar(InscripcionCursada.Crear(Guid.CreateVersion7(), estudiante.Valor.Id, planMateriaId, ahora));
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return materia;
    }

    /// <summary>Darse de baja. No se puede si un profesor ya confirmó su asistencia: así nadie borra un "no asiste".</summary>
    public async Task<Result<bool>> DarseDeBajaAsync(Guid planMateriaId, CancellationToken cancellationToken)
    {
        var estudiante = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!estudiante.EsExitoso)
        {
            return estudiante.Error!;
        }

        var ciclo = InscripcionCursada.CicloActual(clock.UtcNow);
        var inscripcion = await asistencia.ObtenerInscripcionAsync(estudiante.Valor.Id, planMateriaId, ciclo, cancellationToken);
        if (inscripcion is null)
        {
            return ErroresAsistencia.InscripcionNoEncontrada;
        }

        if (await asistencia.TieneConfirmacionesAsync(estudiante.Valor.Id, planMateriaId, ciclo, cancellationToken))
        {
            return ErroresAsistencia.BajaConConfirmaciones;
        }

        asistencia.Eliminar(inscripcion);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return true;
    }
}

/// <summary>El profesor confirma, en un relevamiento abierto, si cada estudiante de sus materias asiste.</summary>
public sealed class ConfirmacionesDocente(UsuarioActual usuarioActual, IAsistenciaRepository asistencia, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<IReadOnlyList<RelevamientoDto>> RelevamientosAsync(CancellationToken cancellationToken)
    {
        var ahora = clock.UtcNow;
        return (await asistencia.ListarRelevamientosAsync(cancellationToken))
            .Where(relevamiento => relevamiento.CicloLectivo == InscripcionCursada.CicloActual(ahora))
            .Select(relevamiento => RelevamientoDto.Desde(relevamiento, ahora))
            .ToList();
    }

    public async Task<Result<PanelDocenteDto>> PanelAsync(Guid relevamientoId, CancellationToken cancellationToken)
    {
        var docente = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!docente.EsExitoso)
        {
            return docente.Error!;
        }

        var relevamiento = await asistencia.ObtenerRelevamientoAsync(relevamientoId, cancellationToken);
        if (relevamiento is null)
        {
            return ErroresAsistencia.RelevamientoNoEncontrado;
        }

        var materias = new List<MateriaAConfirmarDto>();
        foreach (var materia in await asistencia.MateriasDelDocenteAsync(docente.Valor.Id, cancellationToken))
        {
            var estudiantes = await asistencia.EstudiantesDeMateriaAsync(materia.PlanMateriaId, relevamiento.CicloLectivo, cancellationToken);
            var confirmadas = (await asistencia.ConfirmacionesDeMateriaAsync(relevamiento.Id, materia.PlanMateriaId, cancellationToken))
                .ToDictionary(confirmacion => confirmacion.EstudianteId);
            materias.Add(new MateriaAConfirmarDto(
                materia.PlanMateriaId,
                materia.Materia,
                materia.Anio,
                materia.Carrera,
                estudiantes.Select(estudiante => new EstudianteAConfirmarDto(
                    estudiante.Id,
                    estudiante.Nombre,
                    estudiante.Email,
                    confirmadas.GetValueOrDefault(estudiante.Id)?.Valor,
                    confirmadas.GetValueOrDefault(estudiante.Id)?.Observacion)).ToList()));
        }

        return new PanelDocenteDto(RelevamientoDto.Desde(relevamiento, clock.UtcNow), materias);
    }

    public async Task<Result<int>> ConfirmarAsync(Guid relevamientoId, Guid planMateriaId, ConfirmarAsistenciaCommand comando, CancellationToken cancellationToken)
    {
        var docente = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!docente.EsExitoso)
        {
            return docente.Error!;
        }

        var items = comando.Confirmaciones ?? [];
        if (items.Count == 0)
        {
            return ErroresAsistencia.SinConfirmaciones;
        }

        var relevamiento = await asistencia.ObtenerRelevamientoAsync(relevamientoId, cancellationToken);
        if (relevamiento is null)
        {
            return ErroresAsistencia.RelevamientoNoEncontrado;
        }

        var ahora = clock.UtcNow;
        if (!relevamiento.EstaAbierto(ahora))
        {
            return ErroresAsistencia.RelevamientoCerrado;
        }

        // Solo sus materias, y solo estudiantes inscriptos en ellas: un docente no puede opinar sobre alumnos ajenos.
        if (!await asistencia.ExisteAsignacionAsync(docente.Valor.Id, planMateriaId, cancellationToken))
        {
            return ErroresAsistencia.MateriaNoAsignada;
        }

        var inscriptos = (await asistencia.EstudiantesDeMateriaAsync(planMateriaId, relevamiento.CicloLectivo, cancellationToken))
            .Select(estudiante => estudiante.Id)
            .ToHashSet();
        if (items.Any(item => !inscriptos.Contains(item.EstudianteId)))
        {
            return ErroresAsistencia.EstudianteNoInscripto;
        }

        var existentes = (await asistencia.ConfirmacionesDeMateriaAsync(relevamiento.Id, planMateriaId, cancellationToken))
            .ToDictionary(confirmacion => confirmacion.EstudianteId);
        foreach (var item in items.DistinctBy(item => item.EstudianteId))
        {
            if (existentes.TryGetValue(item.EstudianteId, out var existente))
            {
                existente.Actualizar(relevamiento, docente.Valor.Id, item.Valor, item.Observacion, ahora);
            }
            else
            {
                asistencia.Agregar(ConfirmacionAsistencia.Crear(Guid.CreateVersion7(), relevamiento, planMateriaId, item.EstudianteId, docente.Valor.Id, item.Valor, item.Observacion, ahora));
            }
        }

        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return items.DistinctBy(item => item.EstudianteId).Count();
    }
}

/// <summary>
/// La dirección abre un relevamiento, sigue cuántos profesores confirmaron y consulta el informe. El informe se puede filtrar
/// por una lista de DNI (por ejemplo, la de los beneficiarios de la beca): Astrea no guarda quién cobra una beca.
/// El sistema informa; la decisión sigue siendo de la dirección.
/// </summary>
public sealed class RelevamientosAsistencia(UsuarioActual usuarioActual, IAsistenciaRepository asistencia, IUnitOfWork unitOfWork, IClock clock)
{
    public const int MaximoDnis = 500;

    public async Task<IReadOnlyList<RelevamientoDto>> ListarAsync(CancellationToken cancellationToken)
    {
        var ahora = clock.UtcNow;
        return (await asistencia.ListarRelevamientosAsync(cancellationToken)).Select(relevamiento => RelevamientoDto.Desde(relevamiento, ahora)).ToList();
    }

    public async Task<Result<RelevamientoDto>> AbrirAsync(AbrirRelevamientoCommand comando, CancellationToken cancellationToken)
    {
        var admin = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!admin.EsExitoso)
        {
            return admin.Error!;
        }

        var ahora = clock.UtcNow;
        var relevamiento = RelevamientoAsistencia.Abrir(Guid.CreateVersion7(), comando.Titulo ?? string.Empty, comando.FechaCierre, admin.Valor.Id, ahora);
        asistencia.Agregar(relevamiento);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return RelevamientoDto.Desde(relevamiento, ahora);
    }

    public async Task<Result<RelevamientoDto>> CerrarAsync(Guid relevamientoId, CancellationToken cancellationToken)
    {
        var relevamiento = await asistencia.ObtenerRelevamientoAsync(relevamientoId, cancellationToken);
        if (relevamiento is null)
        {
            return ErroresAsistencia.RelevamientoNoEncontrado;
        }

        var ahora = clock.UtcNow;
        if (!relevamiento.EstaAbierto(ahora))
        {
            return ErroresAsistencia.RelevamientoCerrado;
        }

        relevamiento.Cerrar(ahora);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return RelevamientoDto.Desde(relevamiento, ahora);
    }

    public async Task<Result<AvanceRelevamientoDto>> AvanceAsync(Guid relevamientoId, CancellationToken cancellationToken)
    {
        var relevamiento = await asistencia.ObtenerRelevamientoAsync(relevamientoId, cancellationToken);
        if (relevamiento is null)
        {
            return ErroresAsistencia.RelevamientoNoEncontrado;
        }

        var datos = await asistencia.DatosDelInformeAsync(relevamiento, cancellationToken);
        var confirmadas = datos.Confirmaciones.Select(confirmacion => (confirmacion.EstudianteId, confirmacion.PlanMateriaId)).ToHashSet();
        var materias = datos.Inscripciones
            .GroupBy(inscripcion => inscripcion.Materia.PlanMateriaId)
            .Select(grupo =>
            {
                var materia = grupo.First().Materia;
                return new MateriaAvanceDto(
                    materia.PlanMateriaId,
                    materia.Materia,
                    materia.Anio,
                    materia.Carrera,
                    grupo.Count(),
                    grupo.Count(inscripcion => confirmadas.Contains((inscripcion.EstudianteId, materia.PlanMateriaId))),
                    datos.DocentesPorMateria.GetValueOrDefault(materia.PlanMateriaId) ?? []);
            })
            // Primero las que faltan confirmar, para que la dirección sepa a quién recordarle.
            .OrderByDescending(materia => materia.Inscriptos - materia.Confirmados)
            .ThenBy(materia => materia.Carrera, StringComparer.CurrentCulture)
            .ThenBy(materia => materia.Anio)
            .ThenBy(materia => materia.Materia, StringComparer.CurrentCulture)
            .ToList();

        return new AvanceRelevamientoDto(
            RelevamientoDto.Desde(relevamiento, clock.UtcNow),
            datos.Inscripciones.Count,
            datos.Inscripciones.Count(inscripcion => confirmadas.Contains((inscripcion.EstudianteId, inscripcion.Materia.PlanMateriaId))),
            materias);
    }

    public async Task<Result<InformeAsistenciaDto>> InformeAsync(Guid relevamientoId, string? texto, string? dnis, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var relevamiento = await asistencia.ObtenerRelevamientoAsync(relevamientoId, cancellationToken);
        if (relevamiento is null)
        {
            return ErroresAsistencia.RelevamientoNoEncontrado;
        }

        var buscados = (dnis ?? string.Empty)
            .Split([',', ';', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(dni => new string(dni.Where(char.IsAsciiDigit).ToArray()))
            .Where(dni => dni.Length >= 7)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (buscados.Count > MaximoDnis)
        {
            return ErroresAsistencia.DemasiadosDnis;
        }

        var datos = await asistencia.DatosDelInformeAsync(relevamiento, cancellationToken);
        var confirmaciones = datos.Confirmaciones.ToDictionary(confirmacion => (confirmacion.EstudianteId, confirmacion.PlanMateriaId));
        texto = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

        var estudiantes = datos.Inscripciones
            .GroupBy(inscripcion => inscripcion.EstudianteId)
            .Select(grupo => Fila(grupo.ToList(), confirmaciones))
            .Where(estudiante => buscados.Count == 0 || (estudiante.Dni is not null && buscados.Contains(estudiante.Dni)))
            .Where(estudiante => texto is null
                || estudiante.Nombre.Contains(texto, StringComparison.CurrentCultureIgnoreCase)
                || estudiante.Email.Contains(texto, StringComparison.OrdinalIgnoreCase)
                || (estudiante.Dni?.Contains(texto, StringComparison.Ordinal) ?? false))
            // Primero quienes tienen más materias con "no asiste".
            .OrderByDescending(estudiante => estudiante.NoAsiste)
            .ThenByDescending(estudiante => estudiante.Irregular)
            .ThenBy(estudiante => estudiante.Nombre, StringComparer.CurrentCulture)
            .ThenBy(estudiante => estudiante.Id)
            .ToList();

        var encontrados = estudiantes.Select(estudiante => estudiante.Dni).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var pagina = PageRequest.Create(page, pageSize);
        return new InformeAsistenciaDto(
            RelevamientoDto.Desde(relevamiento, clock.UtcNow),
            PagedResult<EstudianteInformeDto>.From(estudiantes.Skip(pagina.Skip).Take(pagina.PageSize).ToList(), pagina, estudiantes.Count),
            buscados.Where(dni => !encontrados.Contains(dni)).ToList());
    }

    private static EstudianteInformeDto Fila(IReadOnlyList<FilaInscripcion> inscripciones, IReadOnlyDictionary<(Guid, Guid), FilaConfirmacion> confirmaciones)
    {
        var primera = inscripciones[0];
        var materias = inscripciones
            .Select(inscripcion =>
            {
                var confirmacion = confirmaciones.GetValueOrDefault((inscripcion.EstudianteId, inscripcion.Materia.PlanMateriaId));
                return new MateriaInformeDto(
                    inscripcion.Materia.Materia,
                    inscripcion.Materia.Carrera,
                    inscripcion.Materia.Anio,
                    confirmacion?.Valor,
                    confirmacion?.Observacion,
                    confirmacion?.Docente,
                    confirmacion?.ConfirmadaEn);
            })
            .OrderBy(materia => materia.Anio)
            .ThenBy(materia => materia.Materia, StringComparer.CurrentCulture)
            .ToList();

        return new EstudianteInformeDto(
            primera.EstudianteId,
            primera.Nombre,
            primera.Email,
            primera.Dni,
            materias,
            materias.Count(materia => materia.Valor == ValorAsistencia.AsisteRegularmente),
            materias.Count(materia => materia.Valor == ValorAsistencia.AsisteIrregularmente),
            materias.Count(materia => materia.Valor == ValorAsistencia.NoAsiste),
            materias.Count(materia => materia.Valor is null));
    }
}
