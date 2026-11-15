using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Astrea.Api.IntegrationTests.Infrastructure;
using Astrea.Domain.Procedures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Api.IntegrationTests;

[Collection(ColeccionApi.Nombre)]
public sealed class TramitesTests(AstreaApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private async Task<HttpClient> SesionAsync(string email)
    {
        var client = factory.CrearClienteCon(TokensDePrueba.Crear($"uid-{Guid.NewGuid():N}", email));
        (await client.PostAsync("/api/v1/auth/sesion", null)).EnsureSuccessStatusCode();
        return client;
    }

    private Task<HttpClient> AdminAsync() => SesionAsync(AstreaApiFactory.AdministradorInicial);

    private Task<HttpClient> EstudianteAsync() => SesionAsync($"alumno-{Guid.NewGuid():N}@astrea.test");

    private static async Task<T> LeerAsync<T>(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<T>(Json))!;

    private static async Task<Guid> CrearCarreraAsync(HttpClient admin, string nombre = "Tecnicatura de Prueba")
    {
        var codigo = $"T{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var carrera = await LeerAsync<IdDto>(await admin.PostAsJsonAsync("/api/v1/admin/carreras", new { codigo, nombre = $"{nombre} {codigo}", duracionAnios = 3 }));
        return carrera.Id;
    }

    private static Task<HttpResponseMessage> SolicitarAsync(HttpClient estudiante, Guid carreraId, Guid? clave = null, string? presentarAnte = "ANSES") =>
        estudiante.PostAsJsonAsync("/api/v1/tramites", new
        {
            tipoId = CatalogoTramites.AlumnoRegularId,
            datos = new Dictionary<string, string?> { ["carrera"] = carreraId.ToString(), ["presentarAnte"] = presentarAnte },
            claveIdempotencia = clave,
        });

    private static async Task<Detalle> SolicitarOkAsync(HttpClient estudiante, Guid carreraId)
    {
        var response = await SolicitarAsync(estudiante, carreraId);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await LeerAsync<Detalle>(response);
    }

    [Fact]
    public async Task El_catalogo_tiene_los_tramites_del_SDD_con_su_formulario()
    {
        var tipos = (await (await EstudianteAsync()).GetFromJsonAsync<List<Tipo>>("/api/v1/tramites/tipos", Json))!;

        tipos.Select(tipo => tipo.Codigo).Should().Contain(["ALUMNO_REGULAR", "CONSTANCIA_EXAMEN", "ANALITICO_PARCIAL", "JUSTIFICACION_INASISTENCIA"]);
        tipos.Single(tipo => tipo.Codigo == "CONSTANCIA_EXAMEN").Campos.Select(campo => campo.Clave)
            .Should().Equal("carrera", "materia", "fechaExamen", "presentarAnte");
    }

    [Fact]
    public async Task El_estudiante_solicita_un_tramite_y_lo_sigue()
    {
        var carreraId = await CrearCarreraAsync(await AdminAsync(), "Desarrollo de Software");
        var estudiante = await EstudianteAsync();

        var response = await SolicitarAsync(estudiante, carreraId);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var creado = await LeerAsync<Detalle>(response);
        response.Headers.Location!.ToString().Should().Be($"/api/v1/tramites/{creado.Id}");
        creado.Numero.Should().MatchRegex(@"^T-\d{6}$");
        creado.Estado.Should().Be("Pendiente");
        creado.Datos.Should().Contain(dato => dato.Etiqueta == "Carrera" && dato.Valor.StartsWith("Desarrollo de Software"));
        creado.Historial.Should().ContainSingle().Which.Actor.Should().Be("Vos");

        var mios = await estudiante.GetFromJsonAsync<Pagina>("/api/v1/me/tramites", Json);
        mios!.Items.Should().ContainSingle().Which.Numero.Should().Be(creado.Numero);
    }

    [Fact]
    public async Task Un_estudiante_no_ve_los_tramites_de_otro()
    {
        var carreraId = await CrearCarreraAsync(await AdminAsync());
        var titular = await EstudianteAsync();
        var otro = await EstudianteAsync();
        var tramite = await SolicitarOkAsync(titular, carreraId);

        (await otro.GetAsync($"/api/v1/tramites/{tramite.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otro.GetFromJsonAsync<Pagina>("/api/v1/me/tramites", Json))!.TotalItems.Should().Be(0);
        (await otro.GetAsync("/api/v1/admin/tramites")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task El_administrador_lo_toma_y_lo_aprueba_y_el_estudiante_ve_el_historial()
    {
        var admin = await AdminAsync();
        var carreraId = await CrearCarreraAsync(admin);
        var estudiante = await EstudianteAsync();
        var tramite = await SolicitarOkAsync(estudiante, carreraId);

        var bandeja = await admin.GetFromJsonAsync<Pagina>($"/api/v1/admin/tramites?texto={tramite.Numero}&estado=Pendiente&estado=EnProceso", Json);
        bandeja!.Items.Should().ContainSingle().Which.Id.Should().Be(tramite.Id);

        (await admin.PostAsync($"/api/v1/admin/tramites/{tramite.Id}/tomar", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var aprobado = await admin.PostAsJsonAsync($"/api/v1/admin/tramites/{tramite.Id}/aprobar", new { comentario = "Retirala en Bedelía" });
        aprobado.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LeerAsync<Detalle>(aprobado)).AdministradorAsignado.Should().Be(AstreaApiFactory.AdministradorInicial);

        var visto = await estudiante.GetFromJsonAsync<Detalle>($"/api/v1/tramites/{tramite.Id}", Json);
        visto!.Estado.Should().Be("Listo");
        visto.ObservacionAdministrativa.Should().Be("Retirala en Bedelía");
        visto.AdministradorAsignado.Should().BeNull();
        visto.Historial.Select(evento => (evento.EstadoNuevo, evento.Actor)).Should().Equal(("Pendiente", "Vos"), ("EnProceso", "Bedelía"), ("Listo", "Bedelía"));
    }

    [Fact]
    public async Task Rechazar_exige_motivo_y_un_tramite_finalizado_no_cambia_mas()
    {
        var admin = await AdminAsync();
        var tramite = await SolicitarOkAsync(await EstudianteAsync(), await CrearCarreraAsync(admin));

        var sinMotivo = await admin.PostAsJsonAsync($"/api/v1/admin/tramites/{tramite.Id}/rechazar", new { motivo = " " });
        var rechazado = await admin.PostAsJsonAsync($"/api/v1/admin/tramites/{tramite.Id}/rechazar", new { motivo = "No figura como alumno regular" });
        var aprobarDespues = await admin.PostAsJsonAsync($"/api/v1/admin/tramites/{tramite.Id}/aprobar", new { comentario = (string?)null });

        sinMotivo.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        rechazado.StatusCode.Should().Be(HttpStatusCode.OK);
        aprobarDespues.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await aprobarDespues.Content.ReadAsStringAsync()).Should().Contain("finalizado");
    }

    [Fact]
    public async Task Dos_administradores_a_la_vez_dejan_un_solo_cambio_registrado()
    {
        var admin = await AdminAsync();
        var otroAdmin = await AdminAsync();
        var tramite = await SolicitarOkAsync(await EstudianteAsync(), await CrearCarreraAsync(admin));

        var respuestas = await Task.WhenAll(
            admin.PostAsJsonAsync($"/api/v1/admin/tramites/{tramite.Id}/aprobar", new { comentario = "Listo" }),
            otroAdmin.PostAsJsonAsync($"/api/v1/admin/tramites/{tramite.Id}/rechazar", new { motivo = "Rechazado" }));

        // Uno gana; el otro recibe 409 (se pisaron) o 400 (llegó cuando ya estaba finalizado).
        respuestas.Count(respuesta => respuesta.StatusCode == HttpStatusCode.OK).Should().Be(1);
        respuestas.Select(respuesta => respuesta.StatusCode).Should().Contain(codigo => codigo == HttpStatusCode.Conflict || codigo == HttpStatusCode.BadRequest);

        await factory.EjecutarEnBaseAsync(async db =>
            (await db.TramiteEventos.CountAsync(evento => evento.TramiteId == tramite.Id)).Should().Be(2));
    }

    [Fact]
    public async Task La_misma_solicitud_enviada_dos_veces_crea_un_solo_tramite()
    {
        var carreraId = await CrearCarreraAsync(await AdminAsync());
        var estudiante = await EstudianteAsync();
        var clave = Guid.NewGuid();

        var primera = await LeerAsync<Detalle>(await SolicitarAsync(estudiante, carreraId, clave));
        var segunda = await LeerAsync<Detalle>(await SolicitarAsync(estudiante, carreraId, clave));

        segunda.Id.Should().Be(primera.Id);
        (await estudiante.GetFromJsonAsync<Pagina>("/api/v1/me/tramites", Json))!.TotalItems.Should().Be(1);
    }

    [Fact]
    public async Task Un_formulario_incompleto_devuelve_400_con_el_campo_que_falta()
    {
        var carreraId = await CrearCarreraAsync(await AdminAsync());

        var response = await SolicitarAsync(await EstudianteAsync(), carreraId, presentarAnte: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Para presentar ante");
    }

    [Fact]
    public async Task Los_numeros_de_tramite_son_correlativos()
    {
        var carreraId = await CrearCarreraAsync(await AdminAsync());
        var estudiante = await EstudianteAsync();

        var primero = await SolicitarOkAsync(estudiante, carreraId);
        var segundo = await SolicitarOkAsync(estudiante, carreraId);

        int.Parse(segundo.Numero[2..]).Should().BeGreaterThan(int.Parse(primero.Numero[2..]));
    }

    private sealed record IdDto(Guid Id);

    private sealed record Campo(string Clave, string Etiqueta, string Tipo, bool Obligatorio);

    private sealed record Tipo(Guid Id, string Codigo, string Nombre, List<Campo> Campos);

    private sealed record Dato(string Clave, string Etiqueta, string Valor);

    private sealed record Evento(string? EstadoAnterior, string EstadoNuevo, string? Comentario, string Actor, DateTimeOffset Fecha);

    private sealed record Detalle(
        Guid Id,
        string Numero,
        string Estado,
        string? ObservacionAdministrativa,
        string? AdministradorAsignado,
        List<Dato> Datos,
        List<Evento> Historial);

    private sealed record Resumen(Guid Id, string Numero, string Estado, string TipoNombre, string SolicitanteEmail);

    private sealed record Pagina(List<Resumen> Items, int TotalItems, bool HasNext);
}
