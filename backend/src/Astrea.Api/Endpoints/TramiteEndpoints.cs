using Astrea.Api.Authorization;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Procedures;
using Astrea.Domain.Procedures;

namespace Astrea.Api.Endpoints;

internal static class TramiteEndpoints
{
    public static RouteGroupBuilder MapTramiteEndpoints(this RouteGroupBuilder api)
    {
        // Estudiante: catálogo, solicitud y seguimiento de sus propios trámites.
        var tramites = api.MapGroup("").WithTags("Trámites");

        tramites.MapGet("/tramites/tipos", (SolicitudesTramite solicitudes, CancellationToken ct) => solicitudes.ListarTiposAsync(ct))
            .WithName("ListarTiposDeTramite")
            .Produces<IReadOnlyList<TramiteTipoDto>>();

        tramites.MapPost("/tramites", async (SolicitarTramiteCommand comando, SolicitudesTramite solicitudes, CancellationToken ct) =>
                (await solicitudes.SolicitarAsync(comando, ct)).ACreado(tramite => $"/api/v1/tramites/{tramite.Id}"))
            .WithName("SolicitarTramite")
            .WithDescription("Crea el trámite a nombre del usuario de la sesión, en estado Pendiente. Con la misma claveIdempotencia devuelve el ya creado.")
            .Produces<TramiteDetalleDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        tramites.MapGet("/tramites/{tramiteId:guid}", async (Guid tramiteId, SolicitudesTramite solicitudes, CancellationToken ct) =>
                (await solicitudes.ObtenerMioAsync(tramiteId, ct)).AHttp())
            .WithName("ObtenerTramite")
            .WithDescription("Solo el titular. Un trámite ajeno responde 404.")
            .Produces<TramiteDetalleDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        tramites.MapPost("/tramites/{tramiteId:guid}/cancelacion", async (Guid tramiteId, SolicitudesTramite solicitudes, CancellationToken ct) =>
                (await solicitudes.CancelarAsync(tramiteId, ct)).AHttp())
            .WithName("CancelarTramite")
            .WithDescription("Solo el titular, mientras esté pendiente o en proceso. Queda en el historial.")
            .Produces<TramiteDetalleDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        tramites.MapGet("/me/tramites", async (EstadoTramite[]? estado, int? page, int? pageSize, SolicitudesTramite solicitudes, CancellationToken ct) =>
                (await solicitudes.ListarMiosAsync(estado, page, pageSize, ct)).AHttp())
            .WithName("ListarMisTramites")
            .Produces<PagedResult<TramiteResumenDto>>();

        // Administración: bandeja y cambios de estado.
        var admin = api.MapGroup("/admin/tramites")
            .RequireAuthorization(Politicas.Administrador)
            .WithTags("Administración: trámites")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        admin.MapGet("", async (EstadoTramite[]? estado, Guid? tipoId, string? texto, int? page, int? pageSize, BandejaTramites bandeja, CancellationToken ct) =>
                (await bandeja.BuscarAsync(estado, tipoId, texto, page, pageSize, ct)).AHttp())
            .WithName("BandejaDeTramites")
            .WithDescription("Los más recientes primero. 'estado' admite varios valores; 'texto' busca por número o por email/apellido del titular.")
            .Produces<PagedResult<TramiteResumenDto>>();

        admin.MapGet("/tipos", (BandejaTramites bandeja, CancellationToken ct) => bandeja.ListarTiposAsync(ct))
            .WithName("AdminListarTiposDeTramite")
            .Produces<IReadOnlyList<TramiteTipoDto>>();

        admin.MapGet("/{tramiteId:guid}", async (Guid tramiteId, BandejaTramites bandeja, CancellationToken ct) =>
                (await bandeja.ObtenerAsync(tramiteId, ct)).AHttp())
            .WithName("AdminObtenerTramite")
            .Produces<TramiteDetalleDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("/{tramiteId:guid}/tomar", async (Guid tramiteId, BandejaTramites bandeja, CancellationToken ct) =>
                (await bandeja.TomarAsync(tramiteId, ct)).AHttp())
            .WithName("TomarTramite")
            .WithDescription("Pendiente → En proceso, asignado al administrador de la sesión.")
            .Produces<TramiteDetalleDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPost("/{tramiteId:guid}/aprobar", async (Guid tramiteId, AprobarTramiteCommand comando, BandejaTramites bandeja, CancellationToken ct) =>
                (await bandeja.AprobarAsync(tramiteId, comando, ct)).AHttp())
            .WithName("AprobarTramite")
            .WithDescription("→ Listo. El comentario (opcional) lo ve el estudiante.")
            .Produces<TramiteDetalleDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPost("/{tramiteId:guid}/rechazar", async (Guid tramiteId, RechazarTramiteCommand comando, BandejaTramites bandeja, CancellationToken ct) =>
                (await bandeja.RechazarAsync(tramiteId, comando, ct)).AHttp())
            .WithName("RechazarTramite")
            .WithDescription("→ Rechazado. El motivo es obligatorio y lo ve el estudiante.")
            .Produces<TramiteDetalleDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapGet("/{tramiteId:guid}/documento", async (Guid tramiteId, BandejaTramites bandeja, CancellationToken ct) =>
                (await bandeja.DescargarDocumentoAsync(tramiteId, ct)).AArchivo())
            .WithName("AdminDescargarDocumento")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("/{tramiteId:guid}/documento/anulacion", async (Guid tramiteId, AnularDocumentoCommand comando, BandejaTramites bandeja, CancellationToken ct) =>
                (await bandeja.AnularDocumentoAsync(tramiteId, comando, ct)).AHttp())
            .WithName("AnularDocumento")
            .WithDescription("El documento deja de descargarse y la validación pública lo muestra como anulado.")
            .Produces<TramiteDetalleDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Estudiante: descarga del PDF de su propio trámite.
        tramites.MapGet("/tramites/{tramiteId:guid}/documento", async (Guid tramiteId, SolicitudesTramite solicitudes, CancellationToken ct) =>
                (await solicitudes.DescargarDocumentoAsync(tramiteId, ct)).AArchivo())
            .WithName("DescargarDocumento")
            .WithDescription("Solo el titular. Un trámite ajeno responde 404.")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Validación pública del QR: sin sesión y con límite de consultas por IP.
        api.MapGet("/documentos/validar/{codigo}", async (string codigo, ValidacionDocumentos validacion, CancellationToken ct) =>
                (await validacion.ValidarAsync(codigo, ct)).AHttp())
            .AllowAnonymous()
            .RequireRateLimiting(LimitesDeUso.ValidacionPublica)
            .WithName("ValidarDocumento")
            .WithTags("Validación de documentos")
            .WithDescription("Datos mínimos: tipo, número de trámite, fecha, estado y últimos 3 dígitos del DNI.")
            .Produces<ValidacionDocumentoDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // Administración: datos oficiales de los estudiantes y plantillas de documentos.
        var gestion = api.MapGroup("/admin")
            .RequireAuthorization(Politicas.Administrador)
            .WithTags("Administración: documentos")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        gestion.MapPut("/usuarios/{usuarioId:guid}/datos-oficiales", async (Guid usuarioId, DatosOficialesCommand comando, DatosOficiales datos, CancellationToken ct) =>
                (await datos.ActualizarAsync(usuarioId, comando, ct)).AHttp())
            .WithName("ActualizarDatosOficiales")
            .WithDescription("Nombre, apellido, DNI y legajo que se imprimen en los documentos.")
            .Produces<PersonaDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        gestion.MapGet("/plantillas", (PlantillasDocumento plantillas, CancellationToken ct) => plantillas.ListarAsync(ct))
            .WithName("ListarPlantillas")
            .Produces<IReadOnlyList<PlantillaDeTipoDto>>();

        gestion.MapPost("/tramites/tipos/{tipoId:guid}/plantillas", async (Guid tipoId, NuevaVersionPlantillaCommand comando, PlantillasDocumento plantillas, CancellationToken ct) =>
                (await plantillas.CrearVersionAsync(tipoId, comando, ct)).ACreado(_ => "/api/v1/admin/plantillas"))
            .WithName("CrearVersionDePlantilla")
            .WithDescription("Crea una versión nueva; las anteriores no se modifican.")
            .Produces<PlantillaDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }
}

/// <summary>Políticas de límite de uso (rate limiting).</summary>
internal static class LimitesDeUso
{
    public const string ValidacionPublica = "validacion-publica";

    public const string Publicaciones = "publicaciones";

    public const string Asistente = "asistente";

    public const string Sesion = "sesion";
}
