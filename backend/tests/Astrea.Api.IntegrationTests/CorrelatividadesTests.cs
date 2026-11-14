using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Astrea.Api.IntegrationTests.Infrastructure;
using AwesomeAssertions;

namespace Astrea.Api.IntegrationTests;

[Collection(ColeccionApi.Nombre)]
public sealed class CorrelatividadesTests(AstreaApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private async Task<HttpClient> SesionAsync(string email)
    {
        var client = factory.CrearClienteCon(TokensDePrueba.Crear($"uid-{Guid.NewGuid():N}", email));
        (await client.PostAsync("/api/v1/auth/sesion", null)).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<T> LeerAsync<T>(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<T>(Json))!;

    /// <summary>Crea una carrera con Programación I, II y III (años 1 a 3) y devuelve el plan y los ids de materia.</summary>
    private static async Task<(Guid PlanId, Guid ProgI, Guid ProgII, Guid ProgIII)> CrearPlanAsync(HttpClient admin)
    {
        var codigo = $"C{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var carrera = await LeerAsync<IdDto>(await admin.PostAsJsonAsync("/api/v1/admin/carreras", new { codigo, nombre = $"Carrera {codigo}", duracionAnios = 3 }));
        var plan = await LeerAsync<IdDto>(await admin.PostAsJsonAsync($"/api/v1/admin/carreras/{carrera.Id}/planes", new { codigoVersion = "Res. 1/26", nombre = "Plan 2026" }));

        async Task<Guid> MateriaAsync(string nombre, int anio) =>
            (await LeerAsync<MateriaDto>(await admin.PostAsJsonAsync($"/api/v1/admin/planes/{plan.Id}/materias", new { nombre, anio, cuatrimestre = "Anual" }))).MateriaId;

        return (plan.Id, await MateriaAsync("Programación I", 1), await MateriaAsync("Programación II", 2), await MateriaAsync("Programación III", 3));
    }

    private static Task<HttpResponseMessage> AgregarAsync(HttpClient admin, Guid planId, object regla) =>
        admin.PostAsJsonAsync($"/api/v1/admin/planes/{planId}/correlatividades", regla);

    [Fact]
    public async Task El_estudiante_ve_requisitos_diferenciados_para_cursar_y_rendir()
    {
        var admin = await SesionAsync(AstreaApiFactory.AdministradorInicial);
        var (planId, progI, progII, _) = await CrearPlanAsync(admin);

        (await AgregarAsync(admin, planId, new { materiaDestinoId = progII, tipo = "Cursar", materiaRequisitoId = progI, condicion = "Regularizada" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await AgregarAsync(admin, planId, new { materiaDestinoId = progII, tipo = "Cursar", condicion = "Aprobada", descripcion = "Todas las materias de primer año aprobadas" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var creada = await AgregarAsync(admin, planId, new { materiaDestinoId = progII, tipo = "Rendir", materiaRequisitoId = progI, condicion = "Aprobada" });
        creada.StatusCode.Should().Be(HttpStatusCode.Created);
        creada.Headers.Location!.ToString().Should().Be($"/api/v1/planes/{planId}/materias/{progII}/correlatividades");

        var estudiante = await SesionAsync($"alumno-{Guid.NewGuid():N}@astrea.test");
        var detalle = await LeerAsync<Detalle>(await estudiante.GetAsync($"/api/v1/planes/{planId}/materias/{progII}/correlatividades"));

        detalle.Nombre.Should().Be("Programación II");
        detalle.ParaCursar.Should().HaveCount(2);
        detalle.ParaCursar[0].Should().Match<Requisito>(r => r.Nombre == "Programación I" && r.Condicion == "Regularizada" && r.Anio == 1);
        detalle.ParaCursar[1].Should().Match<Requisito>(r => r.MateriaId == null && r.Descripcion == "Todas las materias de primer año aprobadas");
        detalle.ParaRendir.Should().ContainSingle().Which.Condicion.Should().Be("Aprobada");
    }

    [Theory]
    [InlineData("Rendir", "Regularizada", "NoCumple", 1)]
    [InlineData("Rendir", "Aprobada", "Cumple", 0)]
    [InlineData("Cursar", "SinCursar", "NoCumple", 1)]
    public async Task La_verificacion_distingue_regularizada_de_aprobada(string tipo, string estado, string resultado, int faltantes)
    {
        var admin = await SesionAsync(AstreaApiFactory.AdministradorInicial);
        var (planId, progI, progII, _) = await CrearPlanAsync(admin);
        await AgregarAsync(admin, planId, new { materiaDestinoId = progII, tipo = "Cursar", materiaRequisitoId = progI, condicion = "Regularizada" });
        await AgregarAsync(admin, planId, new { materiaDestinoId = progII, tipo = "Rendir", materiaRequisitoId = progI, condicion = "Aprobada" });

        var estudiante = await SesionAsync($"alumno-{Guid.NewGuid():N}@astrea.test");
        var response = await estudiante.PostAsJsonAsync(
            $"/api/v1/planes/{planId}/materias/{progII}/correlatividades/verificacion",
            new { tipo, estados = new[] { new { materiaId = progI, estado } } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var verificacion = await LeerAsync<Verificacion>(response);
        verificacion.Resultado.Should().Be(resultado);
        verificacion.Faltantes.Should().HaveCount(faltantes);
    }

    [Fact]
    public async Task Una_cadena_circular_se_rechaza_con_400()
    {
        var admin = await SesionAsync(AstreaApiFactory.AdministradorInicial);
        var (planId, progI, progII, progIII) = await CrearPlanAsync(admin);
        await AgregarAsync(admin, planId, new { materiaDestinoId = progII, tipo = "Rendir", materiaRequisitoId = progI, condicion = "Aprobada" });
        await AgregarAsync(admin, planId, new { materiaDestinoId = progIII, tipo = "Rendir", materiaRequisitoId = progII, condicion = "Aprobada" });

        var response = await AgregarAsync(admin, planId, new { materiaDestinoId = progI, tipo = "Cursar", materiaRequisitoId = progIII, condicion = "Regularizada" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("circular");
    }

    [Fact]
    public async Task El_administrador_quita_una_correlatividad()
    {
        var admin = await SesionAsync(AstreaApiFactory.AdministradorInicial);
        var (planId, progI, progII, _) = await CrearPlanAsync(admin);
        var detalle = await LeerAsync<Detalle>(await AgregarAsync(admin, planId, new { materiaDestinoId = progII, tipo = "Rendir", materiaRequisitoId = progI, condicion = "Aprobada" }));

        var borrado = await admin.DeleteAsync($"/api/v1/admin/planes/{planId}/correlatividades/{detalle.ParaRendir[0].Id}");
        var otraVez = await admin.DeleteAsync($"/api/v1/admin/planes/{planId}/correlatividades/{detalle.ParaRendir[0].Id}");

        borrado.StatusCode.Should().Be(HttpStatusCode.NoContent);
        otraVez.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await LeerAsync<Detalle>(await admin.GetAsync($"/api/v1/admin/planes/{planId}/materias/{progII}/correlatividades"))).ParaRendir.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_estudiante_no_puede_cargar_correlatividades()
    {
        var admin = await SesionAsync(AstreaApiFactory.AdministradorInicial);
        var (planId, progI, progII, _) = await CrearPlanAsync(admin);
        var estudiante = await SesionAsync($"alumno-{Guid.NewGuid():N}@astrea.test");

        var response = await AgregarAsync(estudiante, planId, new { materiaDestinoId = progII, tipo = "Rendir", materiaRequisitoId = progI, condicion = "Aprobada" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Una_materia_de_otro_plan_devuelve_404()
    {
        var admin = await SesionAsync(AstreaApiFactory.AdministradorInicial);
        var (planId, _, _, _) = await CrearPlanAsync(admin);

        (await admin.GetAsync($"/api/v1/planes/{planId}/materias/{Guid.NewGuid()}/correlatividades")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record IdDto(Guid Id);

    private sealed record MateriaDto(Guid PlanMateriaId, Guid MateriaId);

    private sealed record Requisito(Guid Id, string Condicion, int? GrupoAlternativa, Guid? MateriaId, string? Codigo, string? Nombre, int? Anio, string? Descripcion);

    private sealed record Detalle(Guid PlanId, Guid MateriaId, string Nombre, int Anio, List<Requisito> ParaCursar, List<Requisito> ParaRendir);

    private sealed record Verificacion(string Tipo, string Resultado, List<Requisito> Faltantes, List<Requisito> AVerificar);
}
