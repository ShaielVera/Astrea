using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Astrea.Infrastructure.Ai;

/// <summary>
/// Envío a la API de Gemini compartido por los adaptadores: autentica con la API key (en un header, nunca en la URL)
/// y reintenta ante límites de uso (429), errores temporales del servicio (5xx) y fallas de red.
/// </summary>
internal static class GeminiHttp
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Devuelve la última respuesta (exitosa o no). Si la red falla en todos los intentos, relanza esa excepción.
    /// No reintenta si el servicio pide esperar más que <paramref name="esperaMaxima"/>.
    /// </summary>
    public static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient http,
        Uri url,
        string apiKey,
        object pedido,
        int intentos,
        Func<int, TimeSpan> espera,
        TimeSpan esperaMaxima,
        CancellationToken cancellationToken)
    {
        for (var intento = 1; ; intento++)
        {
            using var mensaje = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(pedido, pedido.GetType(), options: Json) };
            mensaje.Headers.Add("x-goog-api-key", apiKey);

            HttpResponseMessage respuesta;
            try
            {
                respuesta = await http.SendAsync(mensaje, cancellationToken);
            }
            catch (Exception ex) when (intento < intentos && EsFallaDeRed(ex, cancellationToken))
            {
                await Task.Delay(espera(intento), cancellationToken);
                continue;
            }

            var temporal = respuesta.StatusCode is HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError;
            var demora = respuesta.Headers.RetryAfter?.Delta ?? espera(intento);
            if (!temporal || intento >= intentos || demora > esperaMaxima)
            {
                return respuesta;
            }

            respuesta.Dispose();
            await Task.Delay(demora, cancellationToken);
        }
    }

    /// <summary>Error de conexión o vencimiento del tiempo de espera (no una cancelación pedida por quien llama).</summary>
    public static bool EsFallaDeRed(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    /// <summary>El mensaje de error que devuelve Gemini, si se puede leer.</summary>
    public static async Task<string?> DetalleDeErrorAsync(HttpResponseMessage respuesta, CancellationToken cancellationToken)
    {
        try
        {
            return (await respuesta.Content.ReadFromJsonAsync<RespuestaError>(Json, cancellationToken))?.Error?.Message;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool RechazoLaApiKey(HttpResponseMessage respuesta, string? detalle) =>
        respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
        || detalle?.Contains("API key", StringComparison.OrdinalIgnoreCase) == true;

    public static string Recortar(string? detalle) =>
        detalle is null ? string.Empty : $": {(detalle.Length > 200 ? detalle[..200] : detalle)}";

    private sealed record RespuestaError(DetalleError? Error);

    private sealed record DetalleError(int Code, string? Message, string? Status);
}
