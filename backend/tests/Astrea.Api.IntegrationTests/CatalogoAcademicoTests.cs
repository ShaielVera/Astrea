using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Astrea.Api.IntegrationTests.Infrastructure;
using AwesomeAssertions;

namespace Astrea.Api.IntegrationTests;

[Collection(ColeccionApi.Nombre)]
public sealed class CatalogoAcademicoTests(AstreaApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static string CodigoUnico() => $"C{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private async Task<HttpClient> SesionAsync(string email)
    {
        var client = factory.CrearClienteCon(TokensDePrueba.Crear($"uid-{Guid.NewGuid():N}", email));
        (await client.PostAsync("/api/v1/auth/sesion", null)).EnsureSuccessStatusCode();
        return client;
    }

    private Task<HttpClient> AdminAsync() => SesionAsync(AstreaApiFactory.AdministradorInicial);

    private Task<HttpClient> EstudianteAsync() => SesionAsync($"alumno-{Guid.NewGuid():N}@astrea.test");

    private static async Task<T> LeerAsync<T>(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<T>(Json))!;

    private static async Task<(Carrera Carrera, Plan Plan)> CrearCarreraConPlanAsync(HttpClient admin, string codigo, int duracion = 3)
    {
        var carreraResponse = await admin.PostAsJsonAsync("/api/v1/admin/carreras", new { codigo, nombre = $"Carrera {codigo}", duracionAnios = duracion });
        carreraResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var carrera = await LeerAsync<Carrera>(carreraResponse);

        var planResponse = await admin.PostAsJsonAsync($"/api/v1/admin/carreras/{carrera.Id}/planes", new { codigoVersion = "Res. 1/26", nombre = "Plan 2026" });
        planResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        return (carrera, await LeerAsync<Plan>(planResponse));
    }

    [Fact]
    public async Task El_administrador_carga_una_carrera_con_plan_y_materias_y_el_estudiante_la_consulta()
    {
        var admin = await AdminAsync();
        var codigo = CodigoUnico();
        var (carrera, plan) = await CrearCarreraConPlanAsync(admin, codigo);

        var materiaResponse = await admin.PostAsJsonAsync(
            $"/api/v1/admin/planes/{plan.Id}/materias",
            new { nombre = "Programación I", anio = 2, cuatrimestre = "Anual", cargaHorariaSemanal = 6 });
        materiaResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var materia = await LeerAsync<MateriaDePlan>(materiaResponse);
        materia.Codigo.Should().Be($"{codigo}-001");

        var estudiante = await EstudianteAsync();
        var carreras = await LeerAsync<Pagina<Carrera>>(await estudiante.GetAsync("/api/v1/carreras?pageSize=50"));
        carreras.Items.Should().Contain(c => c.Id == carrera.Id);

        var planes = await LeerAsync<List<Plan>>(await estudiante.GetAsync($"/api/v1/carreras/{carrera.Id}/planes"));
        planes.Should().ContainSingle().Which.CantidadMaterias.Should().Be(1);

        var materias = await LeerAsync<Pagina<MateriaDePlan>>(await estudiante.GetAsync($"/api/v1/planes/{plan.Id}/materias?anio=2"));
        materias.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(materia);
        materias.Items[0].Cuatrimestre.Should().Be("Anual");
    }

    [Fact]
    public async Task Un_estudiante_no_puede_modificar_el_plan_de_estudios()
    {
        var estudiante = await EstudianteAsync();

        var response = await estudiante.PostAsJsonAsync("/api/v1/admin/carreras", new { codigo = CodigoUnico(), nombre = "Intrusa", duracionAnios = 3 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sin_sesion_el_catalogo_devuelve_401()
    {
        (await factory.CreateClient().GetAsync("/api/v1/carreras")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Un_codigo_de_carrera_repetido_devuelve_409()
    {
        var admin = await AdminAsync();
        var codigo = CodigoUnico();
        await CrearCarreraConPlanAsync(admin, codigo);

        var response = await admin.PostAsJsonAsync("/api/v1/admin/carreras", new { codigo, nombre = "Duplicada", duracionAnios = 3 });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Una_regla_de_negocio_violada_devuelve_400_con_el_motivo()
    {
        var admin = await AdminAsync();
        var (_, plan) = await CrearCarreraConPlanAsync(admin, CodigoUnico(), duracion: 3);

        var response = await admin.PostAsJsonAsync($"/api/v1/admin/planes/{plan.Id}/materias", new { nombre = "Materia de 5.º año", anio = 5, cuatrimestre = "Anual" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problema = await response.Content.ReadFromJsonAsync<JsonElement>();
        problema.GetProperty("detail").GetString().Should().Contain("entre 1 y 3");
    }

    [Fact]
    public async Task Una_carrera_desactivada_desaparece_para_el_estudiante_pero_no_para_el_administrador()
    {
        var admin = await AdminAsync();
        var codigo = CodigoUnico();
        var (carrera, _) = await CrearCarreraConPlanAsync(admin, codigo);

        var update = await admin.PutAsJsonAsync($"/api/v1/admin/carreras/{carrera.Id}", new { codigo, nombre = carrera.Nombre, duracionAnios = 3, activa = false });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var estudiante = await EstudianteAsync();
        (await LeerAsync<Pagina<Carrera>>(await estudiante.GetAsync("/api/v1/carreras?pageSize=50"))).Items.Should().NotContain(c => c.Id == carrera.Id);
        (await estudiante.GetAsync($"/api/v1/carreras/{carrera.Id}/planes")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var paraAdmin = await LeerAsync<Pagina<Carrera>>(await admin.GetAsync("/api/v1/admin/carreras?pageSize=50"));
        paraAdmin.Items.Should().Contain(c => c.Id == carrera.Id && !c.Activa);
    }

    [Fact]
    public async Task El_listado_respeta_la_paginacion_y_el_maximo_de_50()
    {
        var estudiante = await EstudianteAsync();

        var pagina = await LeerAsync<Pagina<Carrera>>(await estudiante.GetAsync("/api/v1/carreras?page=1&pageSize=500"));

        pagina.PageSize.Should().Be(50);
        pagina.Page.Should().Be(1);
    }

    [Fact]
    public async Task Actualizar_una_materia_la_mueve_de_anio()
    {
        var admin = await AdminAsync();
        var (_, plan) = await CrearCarreraConPlanAsync(admin, CodigoUnico());
        var materia = await LeerAsync<MateriaDePlan>(await admin.PostAsJsonAsync(
            $"/api/v1/admin/planes/{plan.Id}/materias", new { nombre = "Base de Datos", anio = 1, cuatrimestre = "Anual" }));

        var response = await admin.PutAsJsonAsync(
            $"/api/v1/admin/planes/{plan.Id}/materias/{materia.PlanMateriaId}",
            new { nombre = "Base de Datos I", anio = 2, cuatrimestre = "Segundo", cargaHorariaSemanal = 4, obligatoria = true, activa = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var actualizada = await LeerAsync<MateriaDePlan>(response);
        actualizada.Nombre.Should().Be("Base de Datos I");
        actualizada.Anio.Should().Be(2);
        actualizada.Cuatrimestre.Should().Be("Segundo");
    }

    private sealed record Pagina<T>(List<T> Items, int Page, int PageSize, int TotalItems, int TotalPages, bool HasPrevious, bool HasNext);

    private sealed record Carrera(Guid Id, string Codigo, string Nombre, int DuracionAnios, bool Activa);

    private sealed record Plan(Guid Id, Guid CarreraId, string CodigoVersion, string Nombre, bool Activo, int CantidadMaterias);

    private sealed record MateriaDePlan(Guid PlanMateriaId, Guid MateriaId, string Codigo, string Nombre, int Anio, string Cuatrimestre, int Orden, int? CargaHorariaSemanal, bool Obligatoria, bool Activa);
}
