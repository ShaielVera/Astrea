using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Astrea.Application.Abstractions.Ai;
using Astrea.Domain.Knowledge;

namespace Astrea.Infrastructure.Ai;

/// <summary>
/// Embeddings con la API de Gemini (batchEmbedContents). Pide vectores de <see cref="FragmentoRag.DimensionesEmbedding"/>
/// dimensiones y los normaliza (Gemini solo los entrega normalizados en la dimensión completa).
/// Reintenta ante límites de uso (429) y errores temporales del servicio.
/// </summary>
internal sealed class GeminiEmbeddingService(HttpClient http, ConfiguracionGemini configuracion) : IEmbeddingService
{
    /// <summary>Máximo de textos por pedido que admite la API.</summary>
    public const int TandaMaxima = 100;

    private const int Intentos = 4;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Espera antes de reintentar; se puede acortar en los tests.</summary>
    internal Func<int, TimeSpan> Espera { get; init; } = intento => TimeSpan.FromSeconds(Math.Pow(2, intento));

    public async Task<IReadOnlyList<float[]>> GenerarAsync(IReadOnlyList<string> textos, PropositoEmbedding proposito, CancellationToken cancellationToken)
    {
        if (textos.Count == 0)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(configuracion.ApiKey))
        {
            throw new EmbeddingNoDisponibleException(
                "Falta configurar la API key de Gemini (Ia:Gemini:ApiKey). Cargala en los secretos del servidor y reindexá el documento.");
        }

        var vectores = new List<float[]>(textos.Count);
        foreach (var tanda in textos.Chunk(TandaMaxima))
        {
            vectores.AddRange(await PedirAsync(tanda, proposito, cancellationToken));
        }

        return vectores;
    }

    private async Task<IReadOnlyList<float[]>> PedirAsync(IReadOnlyList<string> textos, PropositoEmbedding proposito, CancellationToken cancellationToken)
    {
        var modelo = $"models/{configuracion.ModeloEmbeddings}";
        var pedido = new PedidoLote(textos
            .Select(texto => new PedidoEmbedding(
                modelo,
                new Contenido([new Parte(texto)]),
                proposito == PropositoEmbedding.Consulta ? "RETRIEVAL_QUERY" : "RETRIEVAL_DOCUMENT",
                FragmentoRag.DimensionesEmbedding))
            .ToList());
        var url = new Uri(new Uri(configuracion.UrlBase), $"{modelo}:batchEmbedContents");

        for (var intento = 1; ; intento++)
        {
            using var mensaje = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(pedido, options: Json) };
            mensaje.Headers.Add("x-goog-api-key", configuracion.ApiKey);

            HttpResponseMessage respuesta;
            try
            {
                respuesta = await http.SendAsync(mensaje, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                if (intento < Intentos)
                {
                    await Task.Delay(Espera(intento), cancellationToken);
                    continue;
                }

                throw new EmbeddingNoDisponibleException("No se pudo conectar con Gemini. Revisá la conexión a internet y reindexá el documento.", ex);
            }

            using (respuesta)
            {
                if (respuesta.IsSuccessStatusCode)
                {
                    return await LeerVectoresAsync(respuesta, textos.Count, cancellationToken);
                }

                var temporal = respuesta.StatusCode is HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError;
                if (temporal && intento < Intentos)
                {
                    await Task.Delay(respuesta.Headers.RetryAfter?.Delta ?? Espera(intento), cancellationToken);
                    continue;
                }

                throw new EmbeddingNoDisponibleException(await MensajeDeErrorAsync(respuesta, cancellationToken));
            }
        }
    }

    private static async Task<IReadOnlyList<float[]>> LeerVectoresAsync(HttpResponseMessage respuesta, int esperados, CancellationToken cancellationToken)
    {
        var lote = await respuesta.Content.ReadFromJsonAsync<RespuestaLote>(Json, cancellationToken);
        var vectores = lote?.Embeddings?.Select(embedding => embedding.Values).ToList() ?? [];
        if (vectores.Count != esperados || vectores.Any(valores => valores?.Length != FragmentoRag.DimensionesEmbedding))
        {
            throw new EmbeddingNoDisponibleException("Gemini devolvió embeddings con un formato inesperado.");
        }

        return vectores.Select(Vectores.Normalizar).ToList();
    }

    private static async Task<string> MensajeDeErrorAsync(HttpResponseMessage respuesta, CancellationToken cancellationToken)
    {
        string? detalle = null;
        try
        {
            detalle = (await respuesta.Content.ReadFromJsonAsync<RespuestaError>(Json, cancellationToken))?.Error?.Message;
        }
        catch (JsonException)
        {
        }

        if (respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            || detalle?.Contains("API key", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "Gemini rechazó la API key configurada (Ia:Gemini:ApiKey). Revisala y reindexá el documento.";
        }

        if (respuesta.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return "Se alcanzó el límite de uso de Gemini. Esperá unos minutos y reindexá el documento.";
        }

        var texto = detalle is null ? string.Empty : $": {(detalle.Length > 200 ? detalle[..200] : detalle)}";
        return $"Gemini no pudo generar los embeddings (código {(int)respuesta.StatusCode}){texto}";
    }

    private sealed record PedidoLote(IReadOnlyList<PedidoEmbedding> Requests);

    private sealed record PedidoEmbedding(string Model, Contenido Content, string TaskType, int OutputDimensionality);

    private sealed record Contenido(IReadOnlyList<Parte> Parts);

    private sealed record Parte(string Text);

    private sealed record RespuestaLote(IReadOnlyList<ValoresEmbedding>? Embeddings);

    private sealed record ValoresEmbedding(float[] Values);

    private sealed record RespuestaError(DetalleError? Error);

    private sealed record DetalleError(int Code, string? Message, string? Status);
}

/// <summary>Operaciones de vectores compartidas por los adaptadores de embeddings.</summary>
internal static class Vectores
{
    /// <summary>Lleva el vector a largo 1, así la similitud coseno es comparable entre proveedores.</summary>
    public static float[] Normalizar(float[] vector)
    {
        var norma = Math.Sqrt(vector.Sum(valor => (double)valor * valor));
        return norma == 0 ? vector : vector.Select(valor => (float)(valor / norma)).ToArray();
    }
}
