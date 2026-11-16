using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Astrea.Application.Abstractions.Ai;

namespace Astrea.Infrastructure.Ai;

/// <summary>
/// Redacta las respuestas del asistente con Gemini (generateContent). Pide la salida en JSON con un esquema fijo
/// (respuesta, fuentes, sinEvidencia) y temperatura baja para que se apegue a los documentos.
/// Reintenta una sola vez y sin esperas largas: hay una persona esperando la respuesta.
/// </summary>
internal sealed class GeminiModeloLenguaje(HttpClient http, ConfiguracionGemini configuracion) : IModeloLenguaje
{
    private const int Intentos = 2;

    private static readonly EsquemaRespuesta Esquema = new(
        "OBJECT",
        new Dictionary<string, Propiedad>
        {
            ["respuesta"] = new("STRING", null),
            ["fuentes"] = new("ARRAY", new Propiedad("STRING", null)),
            ["sinEvidencia"] = new("BOOLEAN", null),
        },
        ["respuesta", "fuentes", "sinEvidencia"]);

    /// <summary>Espera antes de reintentar; se puede acortar en los tests.</summary>
    internal Func<int, TimeSpan> Espera { get; init; } = _ => TimeSpan.FromSeconds(2);

    public async Task<RespuestaModelo> ResponderAsync(ConsultaModelo consulta, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuracion.ApiKey))
        {
            throw new ModeloNoDisponibleException("Falta configurar la API key de Gemini (Ia:Gemini:ApiKey).");
        }

        var contenidos = consulta.Historial
            .Select(mensaje => new Contenido(mensaje.EsDelUsuario ? "user" : "model", [new Parte(mensaje.Texto)]))
            .Append(new Contenido("user", [new Parte(consulta.Mensaje)]))
            .ToList();
        var pedido = new Pedido(
            new Contenido(null, [new Parte(consulta.Instrucciones)]),
            contenidos,
            new Configuracion(
                Temperature: 0.2,
                ResponseMimeType: "application/json",
                ResponseSchema: Esquema,
                ThinkingConfig: string.IsNullOrWhiteSpace(configuracion.NivelRazonamiento) ? null : new Razonamiento(configuracion.NivelRazonamiento)));
        var url = new Uri(new Uri(configuracion.UrlBase), $"models/{configuracion.ModeloChat}:generateContent");

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await GeminiHttp.EnviarAsync(http, url, configuracion.ApiKey, pedido, Intentos, Espera, TimeSpan.FromSeconds(5), cancellationToken);
        }
        catch (Exception ex) when (GeminiHttp.EsFallaDeRed(ex, cancellationToken))
        {
            throw new ModeloNoDisponibleException("No se pudo conectar con Gemini.", ex);
        }

        using (respuesta)
        {
            if (!respuesta.IsSuccessStatusCode)
            {
                var detalle = await GeminiHttp.DetalleDeErrorAsync(respuesta, cancellationToken);
                throw new ModeloNoDisponibleException(GeminiHttp.RechazoLaApiKey(respuesta, detalle)
                    ? "Gemini rechazó la API key configurada (Ia:Gemini:ApiKey)."
                    : $"Gemini no pudo responder (código {(int)respuesta.StatusCode}){GeminiHttp.Recortar(detalle)}");
            }

            return await LeerAsync(respuesta, cancellationToken);
        }
    }

    private static async Task<RespuestaModelo> LeerAsync(HttpResponseMessage respuesta, CancellationToken cancellationToken)
    {
        var resultado = await respuesta.Content.ReadFromJsonAsync<Resultado>(GeminiHttp.Json, cancellationToken);
        var candidato = resultado?.Candidates?.FirstOrDefault();

        // Gemini bloqueó el pedido o la respuesta por sus filtros: se trata como "no puedo responder eso".
        if (resultado?.PromptFeedback?.BlockReason is not null || candidato?.FinishReason is "SAFETY" or "PROHIBITED_CONTENT" or "BLOCKLIST" or "SPII")
        {
            return new RespuestaModelo(string.Empty, [], SinEvidencia: true);
        }

        // Las partes marcadas como "thought" son el razonamiento interno del modelo, no la respuesta.
        var texto = string.Concat(candidato?.Content?.Parts?.Where(parte => parte.Thought != true).Select(parte => parte.Text) ?? []);
        if (string.IsNullOrWhiteSpace(texto))
        {
            throw new ModeloNoDisponibleException($"Gemini devolvió una respuesta vacía (motivo: {candidato?.FinishReason ?? "desconocido"}).");
        }

        try
        {
            var salida = JsonSerializer.Deserialize<Salida>(texto, GeminiHttp.Json);
            return salida?.Respuesta is null
                ? throw new ModeloNoDisponibleException("Gemini devolvió un JSON sin el campo 'respuesta'.")
                : new RespuestaModelo(salida.Respuesta, salida.Fuentes ?? [], salida.SinEvidencia);
        }
        catch (JsonException ex)
        {
            throw new ModeloNoDisponibleException($"Gemini devolvió un JSON inválido (motivo de fin: {candidato?.FinishReason}).", ex);
        }
    }

    private sealed record Pedido(Contenido SystemInstruction, IReadOnlyList<Contenido> Contents, Configuracion GenerationConfig);

    private sealed record Contenido([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Role, IReadOnlyList<Parte> Parts);

    private sealed record Parte(string Text);

    private sealed record Configuracion(
        double Temperature,
        string ResponseMimeType,
        EsquemaRespuesta ResponseSchema,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Razonamiento? ThinkingConfig);

    private sealed record Razonamiento(string ThinkingLevel);

    private sealed record EsquemaRespuesta(string Type, IReadOnlyDictionary<string, Propiedad> Properties, IReadOnlyList<string> Required);

    private sealed record Propiedad(string Type, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Propiedad? Items);

    private sealed record Resultado(IReadOnlyList<Candidato>? Candidates, Retroalimentacion? PromptFeedback);

    private sealed record Candidato(ContenidoRespuesta? Content, string? FinishReason);

    private sealed record ContenidoRespuesta(IReadOnlyList<ParteRespuesta>? Parts);

    private sealed record ParteRespuesta(string? Text, bool? Thought);

    private sealed record Retroalimentacion(string? BlockReason);

    private sealed record Salida(string? Respuesta, IReadOnlyList<string>? Fuentes, bool SinEvidencia);
}
