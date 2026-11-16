using Astrea.Api.Authorization;
using Astrea.Application.Features.Attendance;

namespace Astrea.Api.Endpoints;

/// <summary>
/// Confirmación de asistencia (Slice 12): Secretaría asigna materias a los docentes, el estudiante declara qué cursa,
/// la dirección abre relevamientos y cada profesor confirma quién asiste en sus materias.
/// </summary>
internal static class AsistenciaEndpoints
{
    public sealed record AsignarMateriaRequest(Guid PlanMateriaId);

    public sealed record InscribirseRequest(Guid PlanMateriaId);

    /// <summary>Los DNI van en el cuerpo (no en la URL) para que no queden en logs ni en el historial.</summary>
    public sealed record InformeRequest(string? Texto, string? Dnis, int? Page, int? PageSize);

    public static RouteGroupBuilder MapAsistenciaEndpoints(this RouteGroupBuilder api)
    {
        var docentes = api.MapGroup("/admin/docentes").RequireAuthorization(Politicas.Administrador).WithTags("Asistencia");

        docentes.MapGet("/candidatos", async (string? texto, GestionDocentes servicio, CancellationToken ct) =>
                (await servicio.BuscarUsuariosAsync(texto, ct)).AHttp())
            .WithName("BuscarUsuariosParaDocente")
            .WithDescription("Usuarios activos por email, nombre, apellido o DNI (al menos 3 caracteres).")
            .Produces<IReadOnlyList<UsuarioEncontradoDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        docentes.MapGet("", (GestionDocentes servicio, CancellationToken ct) => servicio.ListarAsync(ct))
            .WithName("ListarDocentes")
            .Produces<IReadOnlyList<DocenteDto>>();

        docentes.MapPost("/{usuarioId:guid}", async (Guid usuarioId, GestionDocentes servicio, CancellationToken ct) =>
                (await servicio.HacerDocenteAsync(usuarioId, ct)).ASinContenido())
            .WithName("HacerDocente")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        docentes.MapDelete("/{usuarioId:guid}", async (Guid usuarioId, GestionDocentes servicio, CancellationToken ct) =>
                (await servicio.QuitarDocenteAsync(usuarioId, ct)).ASinContenido())
            .WithName("QuitarDocente")
            .WithDescription("Le quita el rol de docente y sus materias. Las confirmaciones que ya hizo se conservan.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        docentes.MapPost("/{usuarioId:guid}/materias", async (Guid usuarioId, AsignarMateriaRequest request, GestionDocentes servicio, CancellationToken ct) =>
                (await servicio.AsignarMateriaAsync(usuarioId, request.PlanMateriaId, ct)).AHttp())
            .WithName("AsignarMateriaADocente")
            .Produces<AsignacionDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        docentes.MapDelete("/asignaciones/{asignacionId:guid}", async (Guid asignacionId, GestionDocentes servicio, CancellationToken ct) =>
                (await servicio.QuitarAsignacionAsync(asignacionId, ct)).ASinContenido())
            .WithName("QuitarMateriaADocente")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var relevamientos = api.MapGroup("/admin/asistencia/relevamientos").RequireAuthorization(Politicas.Administrador).WithTags("Asistencia");

        relevamientos.MapGet("", (RelevamientosAsistencia servicio, CancellationToken ct) => servicio.ListarAsync(ct))
            .WithName("ListarRelevamientos")
            .Produces<IReadOnlyList<RelevamientoDto>>();

        relevamientos.MapPost("", async (AbrirRelevamientoCommand comando, RelevamientosAsistencia servicio, CancellationToken ct) =>
                (await servicio.AbrirAsync(comando, ct)).AHttp())
            .WithName("AbrirRelevamiento")
            .WithDescription("Pide a los profesores que confirmen la asistencia de los estudiantes de sus materias hasta la fecha de cierre.")
            .Produces<RelevamientoDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        relevamientos.MapPost("/{id:guid}/cierre", async (Guid id, RelevamientosAsistencia servicio, CancellationToken ct) =>
                (await servicio.CerrarAsync(id, ct)).AHttp())
            .WithName("CerrarRelevamiento")
            .Produces<RelevamientoDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        relevamientos.MapGet("/{id:guid}/avance", async (Guid id, RelevamientosAsistencia servicio, CancellationToken ct) =>
                (await servicio.AvanceAsync(id, ct)).AHttp())
            .WithName("AvanceDelRelevamiento")
            .WithDescription("Cuántas inscripciones ya confirmaron los profesores, por materia (primero las que faltan).")
            .Produces<AvanceRelevamientoDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        relevamientos.MapPost("/{id:guid}/informe", async (Guid id, InformeRequest request, RelevamientosAsistencia servicio, CancellationToken ct) =>
                (await servicio.InformeAsync(id, request.Texto, request.Dnis, request.Page, request.PageSize, ct)).AHttp())
            .WithName("InformeDeAsistencia")
            .WithDescription(
                "Asistencia confirmada por los profesores, por estudiante y materia. Se puede filtrar por una lista de DNI (hasta 500), " +
                "por ejemplo la de beneficiarios de una beca: Astrea no guarda quién la cobra. El sistema informa; decide la dirección.")
            .Produces<InformeAsistenciaDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var docente = api.MapGroup("/docente/relevamientos").RequireAuthorization(Politicas.Docente).WithTags("Asistencia");

        docente.MapGet("", (ConfirmacionesDocente servicio, CancellationToken ct) => servicio.RelevamientosAsync(ct))
            .WithName("RelevamientosDelDocente")
            .Produces<IReadOnlyList<RelevamientoDto>>();

        docente.MapGet("/{id:guid}", async (Guid id, ConfirmacionesDocente servicio, CancellationToken ct) =>
                (await servicio.PanelAsync(id, ct)).AHttp())
            .WithName("PanelDelDocente")
            .WithDescription("Las materias asignadas al docente con sus estudiantes inscriptos y lo que ya confirmó.")
            .Produces<PanelDocenteDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        docente.MapPost("/{id:guid}/materias/{planMateriaId:guid}/confirmaciones", async (Guid id, Guid planMateriaId, ConfirmarAsistenciaCommand comando, ConfirmacionesDocente servicio, CancellationToken ct) =>
                (await servicio.ConfirmarAsync(id, planMateriaId, comando, ct)).AHttp())
            .WithName("ConfirmarAsistencia")
            .WithDescription("Guarda (o corrige) lo que el profesor confirma de cada estudiante. Solo sus materias, solo estudiantes inscriptos y con el relevamiento abierto.")
            .Produces<int>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var cursada = api.MapGroup("/me").RequireAuthorization(Politicas.Estudiante).WithTags("Asistencia");

        cursada.MapGet("/asistencia", async (CursadaEstudiante servicio, CancellationToken ct) =>
                (await servicio.MiAsistenciaAsync(ct)).AHttp())
            .WithName("MiAsistencia")
            .WithDescription("Las materias que el estudiante cursa este año y lo que confirmaron sus profesores.")
            .Produces<MiAsistenciaDto>();

        cursada.MapPost("/cursada", async (InscribirseRequest request, CursadaEstudiante servicio, CancellationToken ct) =>
                (await servicio.InscribirseAsync(request.PlanMateriaId, ct)).AHttp())
            .WithName("InscribirseACursada")
            .Produces<MateriaCursadaDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        cursada.MapDelete("/cursada/{planMateriaId:guid}", async (Guid planMateriaId, CursadaEstudiante servicio, CancellationToken ct) =>
                (await servicio.DarseDeBajaAsync(planMateriaId, ct)).ASinContenido())
            .WithName("DarseDeBajaDeCursada")
            .WithDescription("No se puede si un profesor ya confirmó la asistencia en esa materia.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return api;
    }
}
