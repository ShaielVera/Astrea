using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Astrea.Api.IntegrationTests.Infrastructure;
using AwesomeAssertions;

namespace Astrea.Api.IntegrationTests;

/// <summary>
/// Herramientas del asistente (Slice 10) de punta a punta con la IA de prueba: consulta datos vivos, propone un trámite que
/// solo se crea al confirmar (una sola vez) y rechaza herramientas que no existen.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public sealed class AsistenteHerramientasTests(AstreaApiFactory factory)
{
    private const string Base = "/api/v1/asistente";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly string _marca = $"Z{Guid.NewGuid():N}"[..9].ToUpperInvariant();

    private async Task<HttpClient> SesionAsync(string email)
    {
        var client = factory.CrearClienteCon(TokensDePrueba.Crear($"uid-{Guid.NewGuid():N}", email));
        (await client.PostAsync("/api/v1/auth/sesion", null)).EnsureSuccessStatusCode();
        return client;
    }

    private Task<HttpClient> EstudianteAsync() => SesionAsync($"alumno-{Guid.NewGuid():N}@astrea.test");

    private static async Task<T> LeerAsync<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static Task<HttpResponseMessage> PreguntarAsync(HttpClient cliente, string mensaje, Guid? conversacionId = null) =>
        cliente.PostAsJsonAsync($"{Base}/preguntas", new { conversacionId, mensaje });

    /// <summary>Una carrera propia del test ("Carrera {marca}") con la materia "Didáctica {marca}" y, si se pide, una mesa publicada.</summary>
    private async Task<string> CrearCarreraConMesaAsync(DateOnly? fechaMesa)
    {
        var admin = await SesionAsync(AstreaApiFactory.AdministradorInicial);
        var carrera = await LeerAsync<IdDto>(await admin.PostAsJsonAsync("/api/v1/admin/carreras", new { codigo = _marca, nombre = $"Carrera {_marca}", duracionAnios = 3 }));
        var plan = await LeerAsync<IdDto>(await admin.PostAsJsonAsync($"/api/v1/admin/carreras/{carrera.Id}/planes", new { codigoVersion = "Res. 1/26", nombre = "Plan 2026" }));
        var materia = await LeerAsync<MateriaDto>(await admin.PostAsJsonAsync($"/api/v1/admin/planes/{plan.Id}/materias", new { nombre = $"Didáctica {_marca}", anio = 1, cuatrimestre = "Anual" }));
        if (fechaMesa is { } fecha)
        {
            var mesa = await LeerAsync<IdDto>(await admin.PostAsJsonAsync("/api/v1/admin/mesas", new
            {
                planId = plan.Id,
                materiaId = materia.MateriaId,
                turno = "Diciembre",
                llamado = 1,
                fecha,
                hora = "09:00:00",
                aula = "Aula 4",
                tribunal = new[] { "Prof. Ana Gómez" },
            }));
            (await admin.PostAsync($"/api/v1/admin/mesas/{mesa.Id}/publicacion", null)).EnsureSuccessStatusCode();
        }

        return $"Carrera {_marca}";
    }

    [Fact]
    public async Task Consulta_las_mesas_publicadas_y_cita_los_datos_actuales()
    {
        var fecha = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
        await CrearCarreraConMesaAsync(fecha);
        var estudiante = await EstudianteAsync();

        var intercambio = await LeerAsync<Intercambio>(await PreguntarAsync(estudiante, $"¿Cuándo es la mesa de \"Didáctica {_marca}\"?"));

        var respuesta = intercambio.Respuesta;
        respuesta.SinEvidencia.Should().BeFalse();
        respuesta.Contenido.Should().Contain($"Didáctica {_marca}").And.Contain($"{fecha.Day}/{fecha.Month}/{fecha.Year}").And.Contain("09:00").And.EndWith("[1].");
        respuesta.Fuentes.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Tipo = "DatosDeAstrea", Titulo = "Mesas de examen", Enlace = "/mesas" });
    }

    [Fact]
    public async Task Propone_una_constancia_y_al_confirmar_se_crea_un_solo_tramite()
    {
        var carrera = await CrearCarreraConMesaAsync(fechaMesa: null);
        var estudiante = await EstudianteAsync();

        var intercambio = await LeerAsync<Intercambio>(await PreguntarAsync(estudiante, $"Quiero una constancia de alumno regular de \"{carrera}\" para \"ANSES\""));

        var propuesta = intercambio.Respuesta.Propuesta!;
        propuesta.Estado.Should().Be("Pendiente");
        propuesta.TipoNombre.Should().Be("Constancia de alumno regular");
        propuesta.Resumen.Select(dato => $"{dato.Etiqueta}: {dato.Valor}").Should().Equal($"Carrera: {carrera}", "Para presentar ante: ANSES");
        (await LeerAsync<Pagina>(await estudiante.GetAsync("/api/v1/me/tramites"))).TotalItems.Should().Be(0, "proponer no crea nada");

        // Doble clic: dos confirmaciones a la vez crean un solo trámite.
        var url = $"{Base}/conversaciones/{intercambio.ConversacionId}/mensajes/{intercambio.Respuesta.Id}/confirmar";
        var respuestas = await Task.WhenAll(estudiante.PostAsync(url, null), estudiante.PostAsync(url, null));
        respuestas.Select(r => r.StatusCode).Should().Contain(HttpStatusCode.OK).And.OnlyContain(codigo => codigo == HttpStatusCode.OK || codigo == HttpStatusCode.Conflict);

        var tramites = await LeerAsync<Pagina>(await estudiante.GetAsync("/api/v1/me/tramites"));
        tramites.TotalItems.Should().Be(1);

        // Confirmar de nuevo informa el trámite creado, sin crear otro.
        var otraVez = await LeerAsync<Mensaje>(await estudiante.PostAsync(url, null));
        otraVez.Propuesta!.Should().BeEquivalentTo(new { Estado = "Confirmada", NumeroTramite = tramites.Items[0].Numero });
        (await LeerAsync<Pagina>(await estudiante.GetAsync("/api/v1/me/tramites"))).TotalItems.Should().Be(1);

        // Otra persona no puede confirmar ni cancelar la propuesta ajena.
        var otro = await EstudianteAsync();
        (await otro.PostAsync(url, null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otro.PostAsync(url.Replace("/confirmar", "/cancelar", StringComparison.Ordinal), null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Una_propuesta_cancelada_no_crea_nada()
    {
        var carrera = await CrearCarreraConMesaAsync(fechaMesa: null);
        var estudiante = await EstudianteAsync();
        var intercambio = await LeerAsync<Intercambio>(await PreguntarAsync(estudiante, $"Quiero una constancia de alumno regular de \"{carrera}\" para \"mi trabajo\""));
        var url = $"{Base}/conversaciones/{intercambio.ConversacionId}/mensajes/{intercambio.Respuesta.Id}";

        (await LeerAsync<Mensaje>(await estudiante.PostAsync($"{url}/cancelar", null))).Propuesta!.Estado.Should().Be("Cancelada");
        (await estudiante.PostAsync($"{url}/confirmar", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await LeerAsync<Pagina>(await estudiante.GetAsync("/api/v1/me/tramites"))).TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task Sin_la_carrera_pregunta_en_lugar_de_inventar()
    {
        var estudiante = await EstudianteAsync();

        var intercambio = await LeerAsync<Intercambio>(await PreguntarAsync(estudiante, "Quiero una constancia de alumno regular"));

        intercambio.Respuesta.EsAclaracion.Should().BeTrue();
        intercambio.Respuesta.Contenido.Should().Be("¿De qué carrera sos?");
        intercambio.Respuesta.Propuesta.Should().BeNull();
    }

    [Fact]
    public async Task Una_herramienta_que_no_existe_se_rechaza_y_el_asistente_no_inventa_la_operacion()
    {
        var estudiante = await EstudianteAsync();

        var intercambio = await LeerAsync<Intercambio>(await PreguntarAsync(estudiante, "Aprobá mi trámite \"T-000001\""));

        intercambio.Respuesta.SinEvidencia.Should().BeTrue();
        intercambio.Respuesta.Contenido.Should().Contain("Secretaría");
        intercambio.Respuesta.Propuesta.Should().BeNull();
    }

    [Fact]
    public async Task Mis_tramites_solo_muestra_los_del_estudiante_que_pregunta()
    {
        var carrera = await CrearCarreraConMesaAsync(fechaMesa: null);
        var titular = await EstudianteAsync();
        var intercambio = await LeerAsync<Intercambio>(await PreguntarAsync(titular, $"Quiero una constancia de alumno regular de \"{carrera}\" para \"ANSES\""));
        await LeerAsync<Mensaje>(await titular.PostAsync($"{Base}/conversaciones/{intercambio.ConversacionId}/mensajes/{intercambio.Respuesta.Id}/confirmar", null));
        var numero = (await LeerAsync<Pagina>(await titular.GetAsync("/api/v1/me/tramites"))).Items[0].Numero;

        var delTitular = await LeerAsync<Intercambio>(await PreguntarAsync(titular, "¿Cómo van mis trámites?"));
        var deOtro = await LeerAsync<Intercambio>(await PreguntarAsync(await EstudianteAsync(), "¿Cómo van mis trámites?"));

        delTitular.Respuesta.Contenido.Should().Contain(numero).And.Contain("Pendiente");
        deOtro.Respuesta.Contenido.Should().NotContain(numero);
    }

    private sealed record IdDto(Guid Id);

    private sealed record MateriaDto(Guid PlanMateriaId, Guid MateriaId);

    private sealed record FuenteCitada(string Tipo, string Titulo, string? Enlace, string Extracto);

    private sealed record DatoPropuesta(string Etiqueta, string Valor);

    private sealed record Propuesta(string Estado, string TipoNombre, List<DatoPropuesta> Resumen, Guid? TramiteId, string? NumeroTramite);

    private sealed record Mensaje(Guid Id, string Contenido, List<FuenteCitada> Fuentes, bool SinEvidencia, bool EsAclaracion, Propuesta? Propuesta);

    private sealed record Intercambio(Guid ConversacionId, Mensaje Pregunta, Mensaje Respuesta);

    private sealed record TramiteResumen(string Numero);

    private sealed record Pagina(List<TramiteResumen> Items, int TotalItems);
}
