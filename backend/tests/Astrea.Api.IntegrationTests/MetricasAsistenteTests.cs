using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Astrea.Api.IntegrationTests.Infrastructure;
using AwesomeAssertions;

namespace Astrea.Api.IntegrationTests;

/// <summary>Panel de métricas del asistente: solo para administradores, con datos agregados y anónimos.</summary>
[Collection(ColeccionApi.Nombre)]
public sealed class MetricasAsistenteTests(AstreaApiFactory factory)
{
    private const string Url = "/api/v1/admin/asistente/metricas";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    // Solo letras: un número largo se ocultaría como DNI.
    private readonly string _marca = string.Concat(Guid.NewGuid().ToString("N").Select(c => (char)('a' + (c % 26))))[..10];

    private async Task<(HttpClient Cliente, string Email)> SesionAsync(string? email = null)
    {
        email ??= $"alumno-{Guid.NewGuid():N}@astrea.test";
        var client = factory.CrearClienteCon(TokensDePrueba.Crear($"uid-{Guid.NewGuid():N}", email));
        (await client.PostAsync("/api/v1/auth/sesion", null)).EnsureSuccessStatusCode();
        return (client, email);
    }

    [Fact]
    public async Task Muestra_temas_frecuentes_y_sin_respuesta_sin_revelar_quien_pregunto()
    {
        var (estudiante, email) = await SesionAsync();
        var (otro, _) = await SesionAsync();

        // El modelo de prueba pide una herramienta prohibida y se abstiene: es un tema sin respuesta.
        foreach (var cliente in new[] { estudiante, otro })
        {
            (await cliente.PostAsJsonAsync("/api/v1/asistente/preguntas", new { mensaje = $"¡Aprobá mi trámite \"{_marca}\"!" })).EnsureSuccessStatusCode();
        }

        (await estudiante.PostAsJsonAsync("/api/v1/asistente/preguntas", new { mensaje = $"Una pregunta única {_marca} de una sola persona" })).EnsureSuccessStatusCode();

        var (admin, _) = await SesionAsync(AstreaApiFactory.AdministradorInicial);
        var respuesta = await admin.GetAsync(Url);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, cuerpo);
        var metricas = JsonSerializer.Deserialize<Metricas>(cuerpo, Json)!;

        var tema = $"aproba mi tramite {_marca}";
        metricas.Frecuentes.Should().ContainSingle(t => t.Tema == tema).Which.Cantidad.Should().Be(2);
        metricas.SinRespuesta.Should().ContainSingle(t => t.Tema == tema).Which.Cantidad.Should().Be(2);
        metricas.Herramientas.Should().Contain(h => h.Nombre == "(no permitida)");
        metricas.Motivos.Should().Contain(m => m.Nombre == "CitasInvalidas" || m.Nombre == "SinInformacion");
        metricas.Resumen.Total.Should().BeGreaterThanOrEqualTo(3);
        metricas.PorDia.Should().HaveCount(30);

        // Anonimato: una pregunta hecha una sola vez no figura entre las frecuentes, y no hay datos de quién preguntó.
        metricas.Frecuentes.Should().NotContain(t => t.Tema.Contains("pregunta unica", StringComparison.Ordinal));
        cuerpo.Should().NotContain(email).And.NotContain("uid-").And.NotContainEquivalentOf("usuario");
    }

    [Fact]
    public async Task Filtra_por_periodo_y_rechaza_periodos_invalidos()
    {
        var (admin, _) = await SesionAsync(AstreaApiFactory.AdministradorInicial);

        var metricas = (await admin.GetFromJsonAsync<Metricas>($"{Url}?desde=2020-01-01&hasta=2020-01-07", Json))!;
        metricas.Resumen.Total.Should().Be(0);
        metricas.PorDia.Should().HaveCount(7).And.OnlyContain(dia => dia.Total == 0);

        (await admin.GetAsync($"{Url}?desde=2026-02-01&hasta=2026-01-01")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.GetAsync($"{Url}?desde=2020-01-01&hasta=2026-01-01")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Un_estudiante_no_puede_ver_las_metricas()
    {
        var (estudiante, _) = await SesionAsync();

        (await estudiante.GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await factory.CreateClient().GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record Resumen(int Total, int Respondidas, int SinEvidencia, int Errores, int? PorcentajeRespondidas);

    private sealed record DiaFila(DateOnly Dia, int Total, int SinRespuesta);

    private sealed record TemaFila(string Tema, int Cantidad);

    private sealed record CantidadFila(string Nombre, int Cantidad);

    private sealed record Metricas(Resumen Resumen, List<DiaFila> PorDia, List<TemaFila> Frecuentes, List<TemaFila> SinRespuesta, List<CantidadFila> Herramientas, List<CantidadFila> Motivos);
}
