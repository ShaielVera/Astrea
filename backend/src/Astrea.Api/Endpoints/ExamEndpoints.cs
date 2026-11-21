using Astrea.Api.Authorization;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Exams;
using Astrea.Domain.Exams;

namespace Astrea.Api.Endpoints;

internal static class ExamEndpoints
{
    public static RouteGroupBuilder MapExamEndpoints(this RouteGroupBuilder api)
    {
        // Consulta para cualquier usuario con sesión: mesas publicadas y canceladas.
        var mesas = api.MapGroup("/mesas").WithTags("Mesas de examen");

        // carreras: se puede repetir (?carreras=a&carreras=b) para ver las mesas de varias carreras juntas.
        mesas.MapGet("", async (DateOnly? desde, DateOnly? hasta, Guid? carreraId, Guid[]? carreras, Guid? materiaId, string? texto, int? page, int? pageSize, ConsultasMesas consultas, CancellationToken ct) =>
                (await consultas.BuscarAsync(new FiltroMesas(desde, hasta, carreraId, materiaId, texto, Carreras: carreras), page, pageSize, ct)).AHttp())
            .WithName("BuscarMesas")
            .WithDescription("Fechas en hora del instituto (Argentina), ambos extremos incluidos. Sin fechas, muestra desde hoy. 'texto' busca en materia y tribunal.")
            .Produces<PagedResult<MesaExamenDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        mesas.MapGet("/{mesaId:guid}", async (Guid mesaId, ConsultasMesas consultas, CancellationToken ct) =>
                (await consultas.ObtenerAsync(mesaId, incluirBorradores: false, ct)).AHttp())
            .WithName("ObtenerMesa")
            .Produces<MesaExamenDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Administración: incluye borradores y permite cargar, publicar, cancelar y eliminar.
        var admin = api.MapGroup("/admin/mesas")
            .RequireAuthorization(Politicas.Administrador)
            .WithTags("Administración: mesas de examen")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        admin.MapGet("", async (DateOnly? desde, DateOnly? hasta, Guid? carreraId, Guid? materiaId, string? texto, EstadoMesa? estado, int? page, int? pageSize, ConsultasMesas consultas, CancellationToken ct) =>
                (await consultas.BuscarParaAdministracionAsync(new FiltroMesas(desde, hasta, carreraId, materiaId, texto, estado), page, pageSize, ct)).AHttp())
            .WithName("AdminBuscarMesas")
            .Produces<PagedResult<MesaExamenDto>>();

        admin.MapGet("/{mesaId:guid}", async (Guid mesaId, ConsultasMesas consultas, CancellationToken ct) =>
                (await consultas.ObtenerAsync(mesaId, incluirBorradores: true, ct)).AHttp())
            .WithName("AdminObtenerMesa")
            .Produces<MesaExamenDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("", async (GuardarMesaCommand comando, AdministracionMesas administracion, CancellationToken ct) =>
                (await administracion.CrearAsync(comando, ct)).ACreado(mesa => $"/api/v1/admin/mesas/{mesa.Id}"))
            .WithName("CrearMesa")
            .WithDescription("La mesa se crea como borrador; los estudiantes la ven recién cuando se publica.")
            .Produces<MesaExamenDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPut("/{mesaId:guid}", async (Guid mesaId, GuardarMesaCommand comando, AdministracionMesas administracion, CancellationToken ct) =>
                (await administracion.ActualizarAsync(mesaId, comando, ct)).AHttp())
            .WithName("ActualizarMesa")
            .Produces<MesaExamenDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPost("/{mesaId:guid}/publicacion", async (Guid mesaId, AdministracionMesas administracion, CancellationToken ct) =>
                (await administracion.PublicarAsync(mesaId, ct)).AHttp())
            .WithName("PublicarMesa")
            .Produces<MesaExamenDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("/{mesaId:guid}/cancelacion", async (Guid mesaId, CancelarMesaCommand comando, AdministracionMesas administracion, CancellationToken ct) =>
                (await administracion.CancelarAsync(mesaId, comando, ct)).AHttp())
            .WithName("CancelarMesa")
            .WithDescription("Solo para mesas publicadas. Sigue visible para los estudiantes, con el motivo.")
            .Produces<MesaExamenDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapDelete("/{mesaId:guid}", async (Guid mesaId, AdministracionMesas administracion, CancellationToken ct) =>
                (await administracion.EliminarAsync(mesaId, ct)).ASinContenido())
            .WithName("EliminarMesa")
            .WithDescription("Solo borradores. Una mesa publicada se cancela.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }
}
