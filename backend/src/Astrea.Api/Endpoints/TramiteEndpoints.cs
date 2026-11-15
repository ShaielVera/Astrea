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
            .WithDescription("Los más antiguos primero. 'estado' admite varios valores; 'texto' busca por número o por email/apellido del titular.")
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

        return api;
    }
}
