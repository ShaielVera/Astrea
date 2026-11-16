using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Astrea.Application.Abstractions.Ai;
using Astrea.Infrastructure.Ai;
using AwesomeAssertions;

namespace Astrea.Infrastructure.IntegrationTests.Ai;

/// <summary>El modelo del asistente contra un servidor simulado de Gemini: no sale a internet ni gasta cuota.</summary>
public sealed class GeminiModeloLenguajeTests
{
    private static readonly ConsultaModelo Consulta = new(
        "Reglas del sistema",
        [new MensajeModelo(true, "¿Cuándo cierra la inscripción?"), new MensajeModelo(false, "Cierra 48 horas antes [1].")],
        "<documentos>…</documentos>\n<pregunta>¿Y en diciembre?</pregunta>",
        [new EvidenciaModelo("F1", "documento=\"Reglamento\"", "texto")]);

    private readonly ServidorSimulado _servidor = new();

    private GeminiModeloLenguaje Modelo(string? apiKey = "clave-de-prueba", string? nivel = "low") =>
        new(new HttpClient(_servidor), new ConfiguracionGemini { ApiKey = apiKey, UrlBase = "https://gemini.test/v1beta/", ModeloChat = "gemini-de-prueba", NivelRazonamiento = nivel })
        {
            Espera = _ => TimeSpan.Zero,
        };

    private static string Respuesta(object salida, string finishReason = "STOP", bool conRazonamiento = false)
    {
        var partes = new List<object>();
        if (conRazonamiento)
        {
            partes.Add(new { text = "Pienso que la respuesta está en F1…", thought = true });
        }

        partes.Add(new { text = JsonSerializer.Serialize(salida) });
        return JsonSerializer.Serialize(new { candidates = new[] { new { content = new { role = "model", parts = partes }, finishReason } } });
    }

    [Fact]
    public async Task Envia_instrucciones_historial_y_pregunta_y_pide_json_con_esquema()
    {
        _servidor.Responder(HttpStatusCode.OK, Respuesta(new { respuesta = "En diciembre también [F1].", fuentes = new[] { "F1" }, sinEvidencia = false }));

        var respuesta = await Modelo().ResponderAsync(Consulta, CancellationToken.None);

        respuesta.Should().BeEquivalentTo(new RespuestaModelo("En diciembre también [F1].", ["F1"], false));
        var pedido = _servidor.Pedidos.Single();
        pedido.Url.Should().Be("https://gemini.test/v1beta/models/gemini-de-prueba:generateContent");
        pedido.ApiKey.Should().Be("clave-de-prueba");
        pedido.Url.Should().NotContain("clave", "la API key nunca va en la URL");

        var cuerpo = JsonNode.Parse(pedido.Cuerpo)!;
        cuerpo["systemInstruction"]!["parts"]![0]!["text"]!.GetValue<string>().Should().Be("Reglas del sistema");
        cuerpo["systemInstruction"]!["role"].Should().BeNull();
        var contenidos = cuerpo["contents"]!.AsArray();
        contenidos.Select(contenido => contenido!["role"]!.GetValue<string>()).Should().Equal("user", "model", "user");
        contenidos[2]!["parts"]![0]!["text"]!.GetValue<string>().Should().Contain("¿Y en diciembre?");

        var configuracion = cuerpo["generationConfig"]!;
        configuracion["temperature"]!.GetValue<double>().Should().BeLessThan(0.5);
        configuracion["responseMimeType"]!.GetValue<string>().Should().Be("application/json");
        configuracion["responseSchema"]!["required"]!.AsArray().Select(campo => campo!.GetValue<string>()).Should().BeEquivalentTo(["respuesta", "fuentes", "sinEvidencia"]);
        configuracion["responseSchema"]!["properties"]!["fuentes"]!["items"]!["type"]!.GetValue<string>().Should().Be("STRING");
        configuracion["thinkingConfig"]!["thinkingLevel"]!.GetValue<string>().Should().Be("low");
    }

    [Fact]
    public async Task Sin_nivel_de_razonamiento_no_envia_esa_configuracion()
    {
        _servidor.Responder(HttpStatusCode.OK, Respuesta(new { respuesta = "Sí [F1].", fuentes = new[] { "F1" }, sinEvidencia = false }));

        await Modelo(nivel: null).ResponderAsync(Consulta, CancellationToken.None);

        JsonNode.Parse(_servidor.Pedidos.Single().Cuerpo)!["generationConfig"]!["thinkingConfig"].Should().BeNull();
    }

    [Fact]
    public async Task Ignora_las_partes_de_razonamiento_interno()
    {
        _servidor.Responder(HttpStatusCode.OK, Respuesta(new { respuesta = "Sí [F1].", fuentes = new[] { "F1" }, sinEvidencia = false }, conRazonamiento: true));

        (await Modelo().ResponderAsync(Consulta, CancellationToken.None)).Respuesta.Should().Be("Sí [F1].");
    }

    [Fact]
    public async Task Respeta_cuando_el_modelo_indica_que_no_hay_evidencia()
    {
        _servidor.Responder(HttpStatusCode.OK, Respuesta(new { respuesta = "No tengo datos.", fuentes = Array.Empty<string>(), sinEvidencia = true }));

        (await Modelo().ResponderAsync(Consulta, CancellationToken.None)).SinEvidencia.Should().BeTrue();
    }

    [Fact]
    public async Task Si_gemini_bloquea_el_pedido_se_trata_como_sin_evidencia()
    {
        _servidor.Responder(HttpStatusCode.OK, """{"promptFeedback":{"blockReason":"PROHIBITED_CONTENT"}}""");

        (await Modelo().ResponderAsync(Consulta, CancellationToken.None)).SinEvidencia.Should().BeTrue();
    }

    [Fact]
    public async Task Reintenta_una_vez_ante_un_error_temporal()
    {
        _servidor.Responder(HttpStatusCode.ServiceUnavailable, """{"error":{"code":503,"message":"overloaded"}}""");
        _servidor.Responder(HttpStatusCode.OK, Respuesta(new { respuesta = "Sí [F1].", fuentes = new[] { "F1" }, sinEvidencia = false }));

        (await Modelo().ResponderAsync(Consulta, CancellationToken.None)).Respuesta.Should().Be("Sí [F1].");
        _servidor.Pedidos.Should().HaveCount(2);
    }

    [Fact]
    public async Task Si_sigue_fallando_avisa_que_no_esta_disponible()
    {
        _servidor.Responder(HttpStatusCode.TooManyRequests, """{"error":{"code":429,"message":"quota"}}""");
        _servidor.Responder(HttpStatusCode.TooManyRequests, """{"error":{"code":429,"message":"quota"}}""");

        var responder = () => Modelo().ResponderAsync(Consulta, CancellationToken.None);

        await responder.Should().ThrowAsync<ModeloNoDisponibleException>().WithMessage("*429*");
        _servidor.Pedidos.Should().HaveCount(2, "con alguien esperando, reintenta una sola vez");
    }

    [Fact]
    public async Task Una_api_key_rechazada_o_faltante_avisa_que_no_esta_disponible()
    {
        _servidor.Responder(HttpStatusCode.BadRequest, """{"error":{"code":400,"message":"API key not valid. Please pass a valid API key."}}""");

        await FluentActions.Awaiting(() => Modelo().ResponderAsync(Consulta, CancellationToken.None)).Should().ThrowAsync<ModeloNoDisponibleException>().WithMessage("*rechazó la API key*");
        await FluentActions.Awaiting(() => Modelo(apiKey: null).ResponderAsync(Consulta, CancellationToken.None)).Should().ThrowAsync<ModeloNoDisponibleException>().WithMessage("*Falta configurar*");
    }

    [Theory]
    [InlineData("esto no es json")]
    [InlineData("{\"fuentes\":[]}")]
    public async Task Una_respuesta_con_formato_invalido_avisa_que_no_esta_disponible(string texto)
    {
        _servidor.Responder(HttpStatusCode.OK, JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text = texto } } }, finishReason = "STOP" } } }));

        await FluentActions.Awaiting(() => Modelo().ResponderAsync(Consulta, CancellationToken.None)).Should().ThrowAsync<ModeloNoDisponibleException>();
    }

    [Fact]
    public async Task El_modelo_de_prueba_responde_con_la_oracion_mas_parecida_y_la_cita()
    {
        var consulta = new ConsultaModelo(
            "reglas",
            [],
            "<documentos>…</documentos>\n<pregunta>\n¿Cuándo cierra la inscripción a mesas?\n</pregunta>",
            [new EvidenciaModelo("F1", "x", "CAPÍTULO I\nLos turnos son tres. La inscripción a mesas cierra 48 horas antes.")]);

        var respuesta = await new ModeloDePrueba().ResponderAsync(consulta, CancellationToken.None);
        var sinEvidencias = await new ModeloDePrueba().ResponderAsync(consulta with { Evidencias = [] }, CancellationToken.None);

        respuesta.Should().BeEquivalentTo(new RespuestaModelo("Según los documentos: La inscripción a mesas cierra 48 horas antes. [F1]", ["F1"], false));
        sinEvidencias.SinEvidencia.Should().BeTrue();
    }
}
