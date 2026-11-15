using Astrea.Api.Authorization;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.LostAndFound;
using Astrea.Domain.Common;
using Astrea.Domain.LostAndFound;

namespace Astrea.Api.Endpoints;

internal static class HallazgoEndpoints
{
    public static RouteGroupBuilder MapHallazgoEndpoints(this RouteGroupBuilder api)
    {
        // Cualquier usuario con sesión: buscar, publicar y seguir sus publicaciones.
        var hallazgos = api.MapGroup("").WithTags("Hallazgos");

        hallazgos.MapGet("/hallazgos", async (string? texto, CategoriaObjeto? categoria, PuntoResguardo? punto, int? page, int? pageSize, Hallazgos servicio, CancellationToken ct) =>
                (await servicio.BuscarAsync(texto, categoria, punto, page, pageSize, ct)).AHttp())
            .WithName("BuscarHallazgos")
            .WithDescription("Solo hallazgos publicados y solo datos públicos. 'texto' busca en el título y el lugar.")
            .Produces<PagedResult<HallazgoPublicoDto>>();

        hallazgos.MapGet("/hallazgos/{id:guid}", async (Guid id, Hallazgos servicio, CancellationToken ct) =>
                (await servicio.ObtenerAsync(id, ct)).AHttp())
            .WithName("ObtenerHallazgo")
            .Produces<HallazgoPublicoDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        hallazgos.MapGet("/hallazgos/{id:guid}/foto", async (Guid id, Hallazgos servicio, CancellationToken ct) =>
                (await servicio.ObtenerFotoAsync(id, ct)).AArchivo())
            .WithName("FotoDeHallazgo")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg")
            .ProducesProblem(StatusCodes.Status404NotFound);

        hallazgos.MapPost("/hallazgos", async (PublicarHallazgoCommand comando, Hallazgos servicio, CancellationToken ct) =>
                (await servicio.PublicarAsync(comando, ct)).ACreado(hallazgo => $"/api/v1/hallazgos/{hallazgo.Id}"))
            .RequireRateLimiting(LimitesDeUso.Publicaciones)
            .WithName("PublicarHallazgo")
            .WithDescription("Publica a nombre del usuario de la sesión. La descripción privada nunca aparece en la búsqueda.")
            .Produces<MiHallazgoDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        hallazgos.MapPut("/hallazgos/{id:guid}/foto", async (Guid id, HttpRequest request, Hallazgos servicio, CancellationToken ct) =>
            {
                var contenido = await LeerCuerpoAsync(request, FotoHallazgo.TamanioMaximoBytes, ct);
                return (await servicio.SubirFotoAsync(id, contenido, request.ContentType, ct)).AHttp();
            })
            .RequireRateLimiting(LimitesDeUso.Publicaciones)
            .Accepts<byte[]>("image/jpeg", "image/png", "image/webp")
            .WithName("SubirFotoDeHallazgo")
            .WithDescription("Cuerpo: la imagen (JPEG, PNG o WebP, hasta 5 MB). Solo quien publicó o la Secretaría.")
            .Produces<MiHallazgoDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        hallazgos.MapGet("/me/hallazgos", async (int? page, int? pageSize, Hallazgos servicio, CancellationToken ct) =>
                (await servicio.ListarMiosAsync(page, pageSize, ct)).AHttp())
            .WithName("MisHallazgos")
            .Produces<PagedResult<MiHallazgoDto>>();

        // Secretaría: ve lo privado, entrega (con verificación presencial) y da de baja.
        var admin = api.MapGroup("/admin/hallazgos")
            .RequireAuthorization(Politicas.Administrador)
            .WithTags("Administración: hallazgos")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        admin.MapGet("", async (EstadoObjeto[]? estado, string? texto, int? page, int? pageSize, AdministracionHallazgos servicio, CancellationToken ct) =>
                (await servicio.BuscarAsync(estado, texto, page, pageSize, ct)).AHttp())
            .WithName("AdminBuscarHallazgos")
            .Produces<PagedResult<HallazgoAdminDto>>();

        admin.MapGet("/{id:guid}", async (Guid id, AdministracionHallazgos servicio, CancellationToken ct) =>
                (await servicio.ObtenerAsync(id, ct)).AHttp())
            .WithName("AdminObtenerHallazgo")
            .Produces<HallazgoAdminDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("/{id:guid}/entrega", async (Guid id, EntregarHallazgoCommand comando, AdministracionHallazgos servicio, CancellationToken ct) =>
                (await servicio.EntregarAsync(id, comando, ct)).AHttp())
            .WithName("EntregarHallazgo")
            .WithDescription("Registra quién lo retira y su DNI. 'identidadVerificada' debe ser true: confirma la verificación presencial.")
            .Produces<HallazgoAdminDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("/{id:guid}/baja", async (Guid id, DarDeBajaHallazgoCommand comando, AdministracionHallazgos servicio, CancellationToken ct) =>
                (await servicio.DarDeBajaAsync(id, comando, ct)).AHttp())
            .WithName("DarDeBajaHallazgo")
            .Produces<HallazgoAdminDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }

    /// <summary>Lee el cuerpo de la petición cortando apenas supera el máximo, sin cargar archivos enormes en memoria.</summary>
    private static async Task<byte[]> LeerCuerpoAsync(HttpRequest request, int maximo, CancellationToken cancellationToken)
    {
        if (request.ContentLength > maximo)
        {
            throw new DomainException($"La foto no puede superar los {maximo / (1024 * 1024)} MB.");
        }

        using var memoria = new MemoryStream();
        var buffer = new byte[81920];
        int leidos;
        while ((leidos = await request.Body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memoria.Length + leidos > maximo)
            {
                throw new DomainException($"La foto no puede superar los {maximo / (1024 * 1024)} MB.");
            }

            memoria.Write(buffer, 0, leidos);
        }

        return memoria.ToArray();
    }
}
