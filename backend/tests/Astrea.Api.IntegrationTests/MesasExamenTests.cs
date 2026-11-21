using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Astrea.Api.IntegrationTests.Infrastructure;
using AwesomeAssertions;

namespace Astrea.Api.IntegrationTests;

[Collection(ColeccionApi.Nombre)]
public sealed class MesasExamenTests(AstreaApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    // Fechas futuras respecto del reloj real de la API, para que siempre se puedan publicar.
    private static readonly DateOnly DiaBase = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60);

    private async Task<HttpClient> SesionAsync(string email)
    {
        var client = factory.CrearClienteCon(TokensDePrueba.Crear($"uid-{Guid.NewGuid():N}", email));
        (await client.PostAsync("/api/v1/auth/sesion", null)).EnsureSuccessStatusCode();
        return client;
    }

    private Task<HttpClient> AdminAsync() => SesionAsync(AstreaApiFactory.AdministradorInicial);

    private Task<HttpClient> EstudianteAsync() => SesionAsync($"alumno-{Guid.NewGuid():N}@astrea.test");

    private static async Task<T> LeerAsync<T>(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<T>(Json))!;

    /// <summary>Crea una carrera propia del test, con un plan y las materias pedidas (en 1.er año).</summary>
    private static async Task<Plan> CrearPlanAsync(HttpClient admin, params string[] materias)
    {
        var codigo = $"M{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var carrera = await LeerAsync<IdDto>(await admin.PostAsJsonAsync("/api/v1/admin/carreras", new { codigo, nombre = $"Carrera {codigo}", duracionAnios = 3 }));
        var plan = await LeerAsync<IdDto>(await admin.PostAsJsonAsync($"/api/v1/admin/carreras/{carrera.Id}/planes", new { codigoVersion = "Res. 1/26", nombre = "Plan 2026" }));

        var ids = new List<Guid>();
        foreach (var nombre in materias)
        {
            var materia = await LeerAsync<MateriaDto>(await admin.PostAsJsonAsync($"/api/v1/admin/planes/{plan.Id}/materias", new { nombre, anio = 1, cuatrimestre = "Anual" }));
            ids.Add(materia.MateriaId);
        }

        return new Plan(carrera.Id, plan.Id, ids);
    }

    private static object NuevaMesa(Plan plan, int materia, DateOnly fecha, string hora = "18:00:00", string[]? tribunal = null, string? desde = null, string? hasta = null) =>
        new
        {
            planId = plan.PlanId,
            materiaId = plan.Materias[materia],
            turno = "Turno de prueba",
            llamado = 1,
            fecha,
            hora,
            aula = "Aula 3",
            tribunal = tribunal ?? ["Prof. Ana Gómez", "Prof. Luis Pérez"],
            inscripcionDesde = desde,
            inscripcionHasta = hasta,
            observaciones = "Traer libreta",
        };

    private static async Task<Mesa> CrearMesaAsync(HttpClient admin, object mesa, bool publicar = true)
    {
        var creada = await admin.PostAsJsonAsync("/api/v1/admin/mesas", mesa);
        creada.StatusCode.Should().Be(HttpStatusCode.Created, await creada.Content.ReadAsStringAsync());
        var dto = await LeerAsync<Mesa>(creada);
        if (!publicar)
        {
            return dto;
        }

        var publicada = await admin.PostAsync($"/api/v1/admin/mesas/{dto.Id}/publicacion", null);
        publicada.StatusCode.Should().Be(HttpStatusCode.OK, await publicada.Content.ReadAsStringAsync());
        return await LeerAsync<Mesa>(publicada);
    }

    private static Task<Pagina> BuscarAsync(HttpClient cliente, string consulta) =>
        cliente.GetFromJsonAsync<Pagina>($"/api/v1/mesas?{consulta}", Json)!;

    [Fact]
    public async Task El_estudiante_ve_la_mesa_recien_cuando_se_publica()
    {
        var admin = await AdminAsync();
        var plan = await CrearPlanAsync(admin, "Programación I");
        var borrador = await CrearMesaAsync(admin, NuevaMesa(plan, 0, DiaBase), publicar: false);
        var estudiante = await EstudianteAsync();

        borrador.Estado.Should().Be("Borrador");
        (await BuscarAsync(estudiante, $"materiaId={plan.Materias[0]}")).TotalItems.Should().Be(0);
        (await estudiante.GetAsync($"/api/v1/mesas/{borrador.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await admin.PostAsync($"/api/v1/admin/mesas/{borrador.Id}/publicacion", null)).EnsureSuccessStatusCode();

        var mesa = (await BuscarAsync(estudiante, $"materiaId={plan.Materias[0]}")).Items.Should().ContainSingle().Subject;
        mesa.Estado.Should().Be("Publicada");
        mesa.MateriaNombre.Should().Be("Programación I");
        mesa.Fecha.Should().Be(DiaBase);
        mesa.Hora.Should().Be("18:00:00");
        mesa.Tribunal.Should().Equal("Prof. Ana Gómez", "Prof. Luis Pérez");
        mesa.Aula.Should().Be("Aula 3");
        mesa.Observaciones.Should().Be("Traer libreta");
    }

    [Fact]
    public async Task Filtra_por_carrera_y_fechas_ordena_por_fecha_y_hora_y_pagina()
    {
        var admin = await AdminAsync();
        var plan = await CrearPlanAsync(admin, "Álgebra", "Biología", "Contabilidad", "Derecho");
        await CrearMesaAsync(admin, NuevaMesa(plan, 0, DiaBase.AddDays(2), "09:00:00"));
        await CrearMesaAsync(admin, NuevaMesa(plan, 1, DiaBase, "18:00:00"));
        await CrearMesaAsync(admin, NuevaMesa(plan, 2, DiaBase, "08:00:00"));
        await CrearMesaAsync(admin, NuevaMesa(plan, 3, DiaBase.AddDays(30)));
        var estudiante = await EstudianteAsync();
        var rango = $"carreraId={plan.CarreraId}&desde={DiaBase:yyyy-MM-dd}&hasta={DiaBase.AddDays(10):yyyy-MM-dd}";

        var primera = await BuscarAsync(estudiante, $"{rango}&page=1&pageSize=2");
        var segunda = await BuscarAsync(estudiante, $"{rango}&page=2&pageSize=2");

        primera.TotalItems.Should().Be(3);
        primera.TotalPages.Should().Be(2);
        primera.HasNext.Should().BeTrue();
        primera.Items.Select(mesa => mesa.MateriaNombre).Should().Equal("Contabilidad", "Biología");
        segunda.Items.Select(mesa => mesa.MateriaNombre).Should().Equal("Álgebra");
        segunda.HasNext.Should().BeFalse();
        segunda.HasPrevious.Should().BeTrue();
    }

    [Fact]
    public async Task Filtra_por_varias_carreras_a_la_vez()
    {
        var admin = await AdminAsync();
        var tsds = await CrearPlanAsync(admin, "Redes");
        var pei = await CrearPlanAsync(admin, "Juego");
        var otra = await CrearPlanAsync(admin, "Química");
        foreach (var plan in new[] { tsds, pei, otra })
        {
            await CrearMesaAsync(admin, NuevaMesa(plan, 0, DiaBase.AddDays(3)));
        }

        var dia = $"desde={DiaBase.AddDays(3):yyyy-MM-dd}&hasta={DiaBase.AddDays(3):yyyy-MM-dd}";
        var mias = await BuscarAsync(await EstudianteAsync(), $"{dia}&carreras={tsds.CarreraId}&carreras={pei.CarreraId}");

        mias.Items.Select(mesa => mesa.MateriaNombre).Should().BeEquivalentTo("Redes", "Juego");
    }

    [Fact]
    public async Task Busca_por_materia_o_tribunal_sin_distinguir_tildes_ni_mayusculas()
    {
        var admin = await AdminAsync();
        var plan = await CrearPlanAsync(admin, "Programación II", "Inglés Técnico");
        await CrearMesaAsync(admin, NuevaMesa(plan, 0, DiaBase, tribunal: ["Prof. Ana Gómez"]));
        await CrearMesaAsync(admin, NuevaMesa(plan, 1, DiaBase, tribunal: ["Prof. John Smith"]));
        var estudiante = await EstudianteAsync();
        var carrera = $"carreraId={plan.CarreraId}";

        (await BuscarAsync(estudiante, $"{carrera}&texto=PROGRAMACION")).Items.Should().ContainSingle().Which.MateriaNombre.Should().Be("Programación II");
        (await BuscarAsync(estudiante, $"{carrera}&texto=gomez")).Items.Should().ContainSingle().Which.MateriaNombre.Should().Be("Programación II");
        (await BuscarAsync(estudiante, $"{carrera}&texto=smith")).Items.Should().ContainSingle().Which.MateriaNombre.Should().Be("Inglés Técnico");
        (await BuscarAsync(estudiante, $"{carrera}&texto=100%25")).TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task Una_mesa_a_las_23_30_queda_en_su_dia_del_instituto_y_no_en_el_dia_UTC()
    {
        var admin = await AdminAsync();
        var plan = await CrearPlanAsync(admin, "Taller nocturno");
        await CrearMesaAsync(admin, NuevaMesa(plan, 0, DiaBase, "23:30:00"));
        var estudiante = await EstudianteAsync();

        var mismoDia = await BuscarAsync(estudiante, $"carreraId={plan.CarreraId}&desde={DiaBase:yyyy-MM-dd}&hasta={DiaBase:yyyy-MM-dd}");
        var diaSiguiente = await BuscarAsync(estudiante, $"carreraId={plan.CarreraId}&desde={DiaBase.AddDays(1):yyyy-MM-dd}");

        var mesa = mismoDia.Items.Should().ContainSingle().Subject;
        mesa.Inicio.Offset.Should().Be(TimeSpan.FromHours(-3));
        mesa.Inicio.UtcDateTime.Should().Be(DiaBase.AddDays(1).ToDateTime(new TimeOnly(2, 30), DateTimeKind.Utc));
        diaSiguiente.TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task Informa_si_la_inscripcion_esta_abierta()
    {
        var admin = await AdminAsync();
        var plan = await CrearPlanAsync(admin, "Física");
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var mesa = await CrearMesaAsync(admin, NuevaMesa(plan, 0, DiaBase, desde: $"{hoy.AddDays(-2):yyyy-MM-dd}", hasta: $"{hoy.AddDays(5):yyyy-MM-dd}"));

        mesa.EstadoInscripcion.Should().Be("Abierta");
    }

    [Fact]
    public async Task No_se_carga_una_mesa_en_una_fecha_que_ya_paso()
    {
        var admin = await AdminAsync();
        var plan = await CrearPlanAsync(admin, "Historia");

        var response = await admin.PostAsJsonAsync("/api/v1/admin/mesas", NuevaMesa(plan, 0, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ya pasaron");
    }

    [Fact]
    public async Task Un_rango_de_fechas_invertido_devuelve_400()
    {
        var estudiante = await EstudianteAsync();

        var response = await estudiante.GetAsync($"/api/v1/mesas?desde={DiaBase:yyyy-MM-dd}&hasta={DiaBase.AddDays(-1):yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Una_mesa_cancelada_sigue_visible_con_su_motivo_y_no_se_puede_eliminar()
    {
        var admin = await AdminAsync();
        var plan = await CrearPlanAsync(admin, "Química");
        var mesa = await CrearMesaAsync(admin, NuevaMesa(plan, 0, DiaBase));

        var cancelada = await admin.PostAsJsonAsync($"/api/v1/admin/mesas/{mesa.Id}/cancelacion", new { motivo = "Paro docente" });
        var eliminar = await admin.DeleteAsync($"/api/v1/admin/mesas/{mesa.Id}");

        cancelada.StatusCode.Should().Be(HttpStatusCode.OK);
        eliminar.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var vista = (await BuscarAsync(await EstudianteAsync(), $"materiaId={plan.Materias[0]}")).Items.Should().ContainSingle().Subject;
        vista.Estado.Should().Be("Cancelada");
        vista.MotivoCancelacion.Should().Be("Paro docente");
    }

    [Fact]
    public async Task Un_borrador_se_elimina()
    {
        var admin = await AdminAsync();
        var plan = await CrearPlanAsync(admin, "Geografía");
        var mesa = await CrearMesaAsync(admin, NuevaMesa(plan, 0, DiaBase), publicar: false);

        (await admin.DeleteAsync($"/api/v1/admin/mesas/{mesa.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetAsync($"/api/v1/admin/mesas/{mesa.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Otra_mesa_de_la_misma_materia_en_el_mismo_horario_devuelve_409()
    {
        var admin = await AdminAsync();
        var plan = await CrearPlanAsync(admin, "Historia");
        await CrearMesaAsync(admin, NuevaMesa(plan, 0, DiaBase));

        var repetida = await admin.PostAsJsonAsync("/api/v1/admin/mesas", NuevaMesa(plan, 0, DiaBase));

        repetida.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Fechas_de_inscripcion_invalidas_devuelven_400()
    {
        var admin = await AdminAsync();
        var plan = await CrearPlanAsync(admin, "Lengua");

        var response = await admin.PostAsJsonAsync("/api/v1/admin/mesas", NuevaMesa(plan, 0, DiaBase, desde: $"{DiaBase.AddDays(-5):yyyy-MM-dd}", hasta: $"{DiaBase:yyyy-MM-dd}"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("antes del día del examen");
    }

    [Fact]
    public async Task Un_estudiante_no_puede_cargar_mesas()
    {
        var admin = await AdminAsync();
        var plan = await CrearPlanAsync(admin, "Arte");

        var response = await (await EstudianteAsync()).PostAsJsonAsync("/api/v1/admin/mesas", NuevaMesa(plan, 0, DiaBase));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record IdDto(Guid Id);

    private sealed record MateriaDto(Guid PlanMateriaId, Guid MateriaId);

    private sealed record Plan(Guid CarreraId, Guid PlanId, List<Guid> Materias);

    private sealed record Mesa(
        Guid Id,
        string Estado,
        DateOnly Fecha,
        string Hora,
        DateTimeOffset Inicio,
        string? Aula,
        List<string> Tribunal,
        string EstadoInscripcion,
        string? Observaciones,
        string? MotivoCancelacion,
        string MateriaNombre);

    private sealed record Pagina(List<Mesa> Items, int Page, int PageSize, int TotalItems, int TotalPages, bool HasPrevious, bool HasNext);
}
