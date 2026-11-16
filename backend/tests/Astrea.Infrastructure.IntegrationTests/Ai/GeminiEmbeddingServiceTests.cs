using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Astrea.Application.Abstractions.Ai;
using Astrea.Domain.Knowledge;
using Astrea.Infrastructure.Ai;
using AwesomeAssertions;

namespace Astrea.Infrastructure.IntegrationTests.Ai;

/// <summary>El adaptador de Gemini contra un servidor simulado: no sale a internet ni gasta cuota.</summary>
public sealed class GeminiEmbeddingServiceTests
{
    private readonly ServidorSimulado _servidor = new();

    private GeminiEmbeddingService Servicio(string? apiKey = "clave-de-prueba") =>
        new(new HttpClient(_servidor), new ConfiguracionGemini { ApiKey = apiKey, UrlBase = "https://gemini.test/v1beta/" })
        {
            Espera = _ => TimeSpan.Zero,
        };

    private static string Respuesta(int cantidad, float valor = 3) =>
        JsonSerializer.Serialize(new { embeddings = Enumerable.Range(0, cantidad).Select(_ => new { values = Enumerable.Repeat(valor, FragmentoRag.DimensionesEmbedding) }) });

    [Fact]
    public async Task Pide_los_embeddings_en_lote_con_la_clave_el_tipo_de_tarea_y_768_dimensiones()
    {
        _servidor.Responder(HttpStatusCode.OK, Respuesta(2));

        var vectores = await Servicio().GenerarAsync(["uno", "dos"], PropositoEmbedding.Documento, CancellationToken.None);

        var pedido = _servidor.Pedidos.Single();
        pedido.Url.Should().Be("https://gemini.test/v1beta/models/gemini-embedding-001:batchEmbedContents");
        pedido.ApiKey.Should().Be("clave-de-prueba");
        var requests = JsonNode.Parse(pedido.Cuerpo)!["requests"]!.AsArray();
        requests.Should().HaveCount(2);
        requests[0]!["taskType"]!.GetValue<string>().Should().Be("RETRIEVAL_DOCUMENT");
        requests[0]!["outputDimensionality"]!.GetValue<int>().Should().Be(768);
        requests[1]!["content"]!["parts"]![0]!["text"]!.GetValue<string>().Should().Be("dos");

        vectores.Should().HaveCount(2);
        Math.Sqrt(vectores[0].Sum(v => (double)v * v)).Should().BeApproximately(1, 1e-5, "los vectores se normalizan");
    }

    [Fact]
    public async Task Las_preguntas_se_piden_como_consulta()
    {
        _servidor.Responder(HttpStatusCode.OK, Respuesta(1));

        await Servicio().GenerarAsync(["¿cuándo cierran las mesas?"], PropositoEmbedding.Consulta, CancellationToken.None);

        JsonNode.Parse(_servidor.Pedidos.Single().Cuerpo)!["requests"]![0]!["taskType"]!.GetValue<string>().Should().Be("RETRIEVAL_QUERY");
    }

    [Fact]
    public async Task Mas_de_100_textos_se_parten_en_varios_pedidos()
    {
        _servidor.Responder(HttpStatusCode.OK, Respuesta(100));
        _servidor.Responder(HttpStatusCode.OK, Respuesta(30));

        var vectores = await Servicio().GenerarAsync(Enumerable.Range(0, 130).Select(i => $"texto {i}").ToList(), PropositoEmbedding.Documento, CancellationToken.None);

        vectores.Should().HaveCount(130);
        _servidor.Pedidos.Should().HaveCount(2);
    }

    [Fact]
    public async Task Reintenta_ante_limite_de_uso_y_errores_temporales()
    {
        _servidor.Responder(HttpStatusCode.TooManyRequests, "{}");
        _servidor.Responder(HttpStatusCode.ServiceUnavailable, "{}");
        _servidor.Responder(HttpStatusCode.OK, Respuesta(1));

        var vectores = await Servicio().GenerarAsync(["uno"], PropositoEmbedding.Documento, CancellationToken.None);

        vectores.Should().HaveCount(1);
        _servidor.Pedidos.Should().HaveCount(3);
    }

    [Fact]
    public async Task Sin_api_key_explica_que_hay_que_configurarla_sin_llamar_a_gemini()
    {
        var generar = () => Servicio(apiKey: null).GenerarAsync(["uno"], PropositoEmbedding.Documento, CancellationToken.None);

        await generar.Should().ThrowAsync<EmbeddingNoDisponibleException>().WithMessage("*Ia:Gemini:ApiKey*");
        _servidor.Pedidos.Should().BeEmpty();
    }

    [Fact]
    public async Task Una_api_key_invalida_no_se_reintenta_y_da_un_mensaje_claro()
    {
        _servidor.Responder(HttpStatusCode.BadRequest, """{"error":{"code":400,"message":"API key not valid. Please pass a valid API key.","status":"INVALID_ARGUMENT"}}""");

        var generar = () => Servicio().GenerarAsync(["uno"], PropositoEmbedding.Documento, CancellationToken.None);

        await generar.Should().ThrowAsync<EmbeddingNoDisponibleException>().WithMessage("*rechazó la API key*");
        _servidor.Pedidos.Should().ContainSingle();
    }

    [Fact]
    public async Task Una_respuesta_con_otra_dimension_se_rechaza()
    {
        _servidor.Responder(HttpStatusCode.OK, JsonSerializer.Serialize(new { embeddings = new[] { new { values = new float[3072] } } }));

        var generar = () => Servicio().GenerarAsync(["uno"], PropositoEmbedding.Documento, CancellationToken.None);

        await generar.Should().ThrowAsync<EmbeddingNoDisponibleException>().WithMessage("*formato inesperado*");
    }
}
