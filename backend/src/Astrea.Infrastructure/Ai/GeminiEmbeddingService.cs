using System.Net;
using System.Net.Http.Json;
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

    /// <summary>
    /// Los documentos se mandan de a pocos fragmentos: el plan gratis de Gemini limita el texto por minuto, y una tanda de 100
    /// fragmentos de un reglamento largo lo supera de una sola vez (y fallaría siempre, por más que se reintente).
    /// </summary>
    public const int TandaDocumentos = 20;

    private const int Intentos = 4;

    /// <summary>Al indexar no hay nadie esperando: ante un límite de uso se reintenta más veces y esperando más.</summary>
    private const int IntentosDocumentos = 6;

    /// <summary>Espera antes de reintentar una consulta (alguien está esperando la respuesta); se puede acortar en los tests.</summary>
    internal Func<int, TimeSpan> Espera { get; init; } = intento => TimeSpan.FromSeconds(Math.Pow(2, intento));

    /// <summary>Espera antes de reintentar al indexar: 10, 20, 40 y 60 segundos, para que se renueve el límite por minuto.</summary>
    internal Func<int, TimeSpan> EsperaDocumentos { get; init; } = intento => TimeSpan.FromSeconds(Math.Min(60, 10 * Math.Pow(2, intento - 1)));

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
        var tamanio = proposito == PropositoEmbedding.Documento ? TandaDocumentos : TandaMaxima;
        foreach (var tanda in textos.Chunk(tamanio))
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

        HttpResponseMessage respuesta;
        try
        {
            var esDocumento = proposito == PropositoEmbedding.Documento;
            respuesta = await GeminiHttp.EnviarAsync(
                http,
                url,
                configuracion.ApiKey!,
                pedido,
                esDocumento ? IntentosDocumentos : Intentos,
                esDocumento ? EsperaDocumentos : Espera,
                TimeSpan.FromMinutes(1),
                cancellationToken);
        }
        catch (Exception ex) when (GeminiHttp.EsFallaDeRed(ex, cancellationToken))
        {
            throw new EmbeddingNoDisponibleException("No se pudo conectar con Gemini. Revisá la conexión a internet y reindexá el documento.", ex);
        }

        using (respuesta)
        {
            if (respuesta.IsSuccessStatusCode)
            {
                return await LeerVectoresAsync(respuesta, textos.Count, cancellationToken);
            }

            throw new EmbeddingNoDisponibleException(await MensajeDeErrorAsync(respuesta, cancellationToken));
        }
    }

    private static async Task<IReadOnlyList<float[]>> LeerVectoresAsync(HttpResponseMessage respuesta, int esperados, CancellationToken cancellationToken)
    {
        var lote = await respuesta.Content.ReadFromJsonAsync<RespuestaLote>(GeminiHttp.Json, cancellationToken);
        var vectores = lote?.Embeddings?.Select(embedding => embedding.Values).ToList() ?? [];
        if (vectores.Count != esperados || vectores.Any(valores => valores?.Length != FragmentoRag.DimensionesEmbedding))
        {
            throw new EmbeddingNoDisponibleException("Gemini devolvió embeddings con un formato inesperado.");
        }

        return vectores.Select(Vectores.Normalizar).ToList();
    }

    private static async Task<string> MensajeDeErrorAsync(HttpResponseMessage respuesta, CancellationToken cancellationToken)
    {
        var detalle = await GeminiHttp.DetalleDeErrorAsync(respuesta, cancellationToken);
        if (GeminiHttp.RechazoLaApiKey(respuesta, detalle))
        {
            return "Gemini rechazó la API key configurada (Ia:Gemini:ApiKey). Revisala y reindexá el documento.";
        }

        if (respuesta.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return "Se alcanzó el límite de uso de Gemini. Esperá unos minutos y reindexá el documento.";
        }

        return $"Gemini no pudo generar los embeddings (código {(int)respuesta.StatusCode}){GeminiHttp.Recortar(detalle)}";
    }

    private sealed record PedidoLote(IReadOnlyList<PedidoEmbedding> Requests);

    private sealed record PedidoEmbedding(string Model, Contenido Content, string TaskType, int OutputDimensionality);

    private sealed record Contenido(IReadOnlyList<Parte> Parts);

    private sealed record Parte(string Text);

    private sealed record RespuestaLote(IReadOnlyList<ValoresEmbedding>? Embeddings);

    private sealed record ValoresEmbedding(float[] Values);
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
