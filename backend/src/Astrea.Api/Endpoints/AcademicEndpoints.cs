using Astrea.Api.Authorization;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Academic;

namespace Astrea.Api.Endpoints;

internal static class AcademicEndpoints
{
    public static RouteGroupBuilder MapAcademicEndpoints(this RouteGroupBuilder api)
    {
        // Consulta para cualquier usuario con sesión: solo carreras, planes y materias activos.
        var catalogo = api.MapGroup("").WithTags("Catálogo académico");

        catalogo.MapGet("/carreras", (ConsultasCatalogo consultas, int? page, int? pageSize, CancellationToken ct) =>
                consultas.ListarCarrerasAsync(page, pageSize, incluirInactivas: false, ct))
            .WithName("ListarCarreras")
            .Produces<PagedResult<CarreraDto>>();

        catalogo.MapGet("/carreras/{carreraId:guid}/planes", async (Guid carreraId, ConsultasCatalogo consultas, CancellationToken ct) =>
                (await consultas.ListarPlanesAsync(carreraId, incluirInactivos: false, ct)).AHttp())
            .WithName("ListarPlanesDeCarrera")
            .Produces<IReadOnlyList<PlanEstudioDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        catalogo.MapGet("/planes/{planId:guid}/materias", async (Guid planId, int? anio, int? page, int? pageSize, ConsultasCatalogo consultas, CancellationToken ct) =>
                (await consultas.ListarMateriasAsync(planId, anio, page, pageSize, incluirInactivas: false, ct)).AHttp())
            .WithName("ListarMateriasDePlan")
            .Produces<PagedResult<MateriaDePlanDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Administración: incluye lo inactivo y permite altas y modificaciones.
        var admin = api.MapGroup("/admin")
            .RequireAuthorization(Politicas.Administrador)
            .WithTags("Administración: plan de estudios")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        admin.MapGet("/carreras", (ConsultasCatalogo consultas, int? page, int? pageSize, CancellationToken ct) =>
                consultas.ListarCarrerasAsync(page, pageSize, incluirInactivas: true, ct))
            .WithName("AdminListarCarreras");

        admin.MapPost("/carreras", async (GuardarCarreraCommand comando, AdministracionCatalogo administracion, CancellationToken ct) =>
                (await administracion.CrearCarreraAsync(comando, ct)).ACreado(carrera => $"/api/v1/carreras/{carrera.Id}/planes"))
            .WithName("CrearCarrera")
            .Produces<CarreraDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPut("/carreras/{carreraId:guid}", async (Guid carreraId, GuardarCarreraCommand comando, AdministracionCatalogo administracion, CancellationToken ct) =>
                (await administracion.ActualizarCarreraAsync(carreraId, comando, ct)).AHttp())
            .WithName("ActualizarCarrera")
            .Produces<CarreraDto>();

        admin.MapGet("/carreras/{carreraId:guid}/planes", async (Guid carreraId, ConsultasCatalogo consultas, CancellationToken ct) =>
                (await consultas.ListarPlanesAsync(carreraId, incluirInactivos: true, ct)).AHttp())
            .WithName("AdminListarPlanes");

        admin.MapPost("/carreras/{carreraId:guid}/planes", async (Guid carreraId, GuardarPlanCommand comando, AdministracionCatalogo administracion, CancellationToken ct) =>
                (await administracion.CrearPlanAsync(carreraId, comando, ct)).ACreado(plan => $"/api/v1/planes/{plan.Id}/materias"))
            .WithName("CrearPlan")
            .Produces<PlanEstudioDto>(StatusCodes.Status201Created);

        admin.MapPut("/planes/{planId:guid}", async (Guid planId, GuardarPlanCommand comando, AdministracionCatalogo administracion, CancellationToken ct) =>
                (await administracion.ActualizarPlanAsync(planId, comando, ct)).AHttp())
            .WithName("ActualizarPlan")
            .Produces<PlanEstudioDto>();

        admin.MapGet("/planes/{planId:guid}/materias", async (Guid planId, int? anio, int? page, int? pageSize, ConsultasCatalogo consultas, CancellationToken ct) =>
                (await consultas.ListarMateriasAsync(planId, anio, page, pageSize, incluirInactivas: true, ct)).AHttp())
            .WithName("AdminListarMaterias");

        admin.MapPost("/planes/{planId:guid}/materias", async (Guid planId, AgregarMateriaCommand comando, AdministracionCatalogo administracion, CancellationToken ct) =>
                (await administracion.AgregarMateriaAsync(planId, comando, ct)).ACreado(_ => $"/api/v1/planes/{planId}/materias"))
            .WithName("AgregarMateria")
            .Produces<MateriaDePlanDto>(StatusCodes.Status201Created);

        admin.MapPut("/planes/{planId:guid}/materias/{planMateriaId:guid}", async (Guid planId, Guid planMateriaId, ActualizarMateriaCommand comando, AdministracionCatalogo administracion, CancellationToken ct) =>
                (await administracion.ActualizarMateriaAsync(planId, planMateriaId, comando, ct)).AHttp())
            .WithName("ActualizarMateria")
            .Produces<MateriaDePlanDto>();

        return api;
    }
}
