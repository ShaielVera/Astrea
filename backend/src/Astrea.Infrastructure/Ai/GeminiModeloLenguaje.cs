using System.Text.Json;
using System.Text.Json.Nodes;
using Astrea.Application.Abstractions.Ai;

namespace Astrea.Infrastructure.Ai;

/// <summary>
/// El asistente con Gemini (generateContent con function calling). En modo "ANY" el modelo siempre responde proponiendo
/// llamadas a herramientas (la respuesta final también es una: "responder"), así la salida siempre es estructurada.
/// Reintenta una sola vez y sin esperas largas: hay una persona esperando la respuesta.
/// </summary>
internal sealed class GeminiModeloLenguaje(HttpClient http, ConfiguracionGemini configuracion) : IModeloLenguaje
{
    private const int Intentos = 2;

    /// <summary>Motivos de fin por los que Gemini bloqueó la respuesta con sus filtros.</summary>
    private static readonly HashSet<string> Bloqueos = ["SAFETY", "PROHIBITED_CONTENT", "BLOCKLIST", "SPII", "RECITATION"];

    /// <summary>Espera antes de reintentar; se puede acortar en los tests.</summary>
    internal Func<int, TimeSpan> Espera { get; init; } = _ => TimeSpan.FromSeconds(2);

    public async Task<TurnoLlamadas> SiguientePasoAsync(ConsultaAgente consulta, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuracion.ApiKey))
        {
            throw new ModeloNoDisponibleException("Falta configurar la API key de Gemini (Ia:Gemini:ApiKey).");
        }

        var url = new Uri(new Uri(configuracion.UrlBase), $"models/{configuracion.ModeloChat}:generateContent");
        HttpResponseMessage respuesta;
        try
        {
            respuesta = await GeminiHttp.EnviarAsync(http, url, configuracion.ApiKey, Pedido(consulta), Intentos, Espera, TimeSpan.FromSeconds(5), cancellationToken);
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

            JsonNode? cuerpo;
            try
            {
                cuerpo = JsonNode.Parse(await respuesta.Content.ReadAsStringAsync(cancellationToken));
            }
            catch (JsonException ex)
            {
                throw new ModeloNoDisponibleException("Gemini devolvió una respuesta que no es JSON.", ex);
            }

            return Leer(cuerpo);
        }
    }

    private JsonObject Pedido(ConsultaAgente consulta)
    {
        var generacion = new JsonObject { ["temperature"] = 0.2 };
        if (!string.IsNullOrWhiteSpace(configuracion.NivelRazonamiento))
        {
            generacion["thinkingConfig"] = new JsonObject { ["thinkingLevel"] = configuracion.NivelRazonamiento };
        }

        return new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(Texto(consulta.Instrucciones)) },
            ["contents"] = new JsonArray(consulta.Turnos.Select(Contenido).ToArray<JsonNode?>()),
            ["tools"] = new JsonArray(new JsonObject
            {
                ["functionDeclarations"] = new JsonArray(consulta.Herramientas.Select(Declaracion).ToArray<JsonNode?>()),
            }),
            // ANY: el modelo siempre llama a una herramienta (incluida "responder"), nunca contesta texto suelto.
            ["toolConfig"] = new JsonObject { ["functionCallingConfig"] = new JsonObject { ["mode"] = "ANY" } },
            ["generationConfig"] = generacion,
        };
    }

    private static JsonNode Contenido(TurnoAgente turno) => turno switch
    {
        TurnoUsuario usuario => new JsonObject { ["role"] = "user", ["parts"] = new JsonArray(Texto(usuario.Texto)) },
        TurnoModelo modelo => new JsonObject { ["role"] = "model", ["parts"] = new JsonArray(Texto(modelo.Texto)) },

        // El contenido original incluye las "thought signatures" que Gemini exige recibir de vuelta sin cambios.
        TurnoLlamadas { Original: JsonNode original } => original.DeepClone(),
        TurnoLlamadas llamadas => new JsonObject
        {
            ["role"] = "model",
            ["parts"] = new JsonArray(llamadas.Llamadas.Select(llamada => (JsonNode?)new JsonObject
            {
                ["functionCall"] = new JsonObject { ["name"] = llamada.Nombre, ["args"] = JsonNode.Parse(llamada.Argumentos.GetRawText()) },
            }).ToArray()),
        },
        TurnoResultados resultados => new JsonObject
        {
            ["role"] = "user",
            ["parts"] = new JsonArray(resultados.Resultados.Select(resultado => (JsonNode?)new JsonObject
            {
                ["functionResponse"] = new JsonObject
                {
                    ["name"] = resultado.Nombre,
                    ["response"] = new JsonObject { ["resultado"] = JsonSerializer.SerializeToNode(resultado.Contenido, resultado.Contenido.GetType(), GeminiHttp.Json) },
                },
            }).ToArray()),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(turno), turno.GetType().Name, "Turno desconocido."),
    };

    private static JsonObject Texto(string texto) => new() { ["text"] = texto };

    private static JsonObject Declaracion(DefinicionHerramienta herramienta) => new()
    {
        ["name"] = herramienta.Nombre,
        ["description"] = herramienta.Descripcion,
        ["parameters"] = Objeto(herramienta.Parametros),
    };

    private static JsonObject Objeto(IReadOnlyList<ParametroHerramienta> parametros)
    {
        var propiedades = new JsonObject();
        foreach (var parametro in parametros)
        {
            propiedades[parametro.Nombre] = Esquema(parametro);
        }

        return new JsonObject
        {
            ["type"] = "OBJECT",
            ["properties"] = propiedades,
            ["required"] = new JsonArray(parametros.Where(p => p.Obligatorio).Select(p => (JsonNode?)p.Nombre).ToArray()),
        };
    }

    private static JsonObject Esquema(ParametroHerramienta parametro)
    {
        var esquema = parametro.Tipo switch
        {
            TipoParametro.Booleano => new JsonObject { ["type"] = "BOOLEAN" },
            TipoParametro.ListaDeTextos => new JsonObject { ["type"] = "ARRAY", ["items"] = new JsonObject { ["type"] = "STRING" } },
            TipoParametro.ListaDeObjetos => new JsonObject { ["type"] = "ARRAY", ["items"] = Objeto(parametro.Campos ?? []) },
            _ => new JsonObject { ["type"] = "STRING" },
        };
        esquema["description"] = parametro.Descripcion;
        if (parametro.Valores is { Count: > 0 } valores)
        {
            esquema["enum"] = new JsonArray(valores.Select(valor => (JsonNode?)valor).ToArray());
        }

        return esquema;
    }

    private static TurnoLlamadas Leer(JsonNode? cuerpo)
    {
        var candidato = cuerpo?["candidates"]?[0];

        // Gemini bloqueó el pedido o la respuesta con sus filtros: no hay llamadas, el asistente se abstiene.
        if (cuerpo?["promptFeedback"]?["blockReason"] is not null || (candidato?["finishReason"]?.GetValue<string>() is { } motivo && Bloqueos.Contains(motivo)))
        {
            return new TurnoLlamadas([]);
        }

        var contenido = candidato?["content"] as JsonObject;
        var partes = contenido?["parts"] as JsonArray;
        if (contenido is null || partes is null)
        {
            throw new ModeloNoDisponibleException($"Gemini devolvió una respuesta vacía (motivo: {candidato?["finishReason"] ?? "desconocido"}).");
        }

        var llamadas = new List<LlamadaHerramienta>();
        foreach (var parte in partes)
        {
            if (parte?["functionCall"] is JsonObject llamada && llamada["name"]?.GetValue<string>() is { Length: > 0 } nombre)
            {
                var argumentos = JsonSerializer.Deserialize<JsonElement>(llamada["args"]?.ToJsonString() ?? "{}");
                llamadas.Add(new LlamadaHerramienta(nombre, argumentos));
            }
        }

        return new TurnoLlamadas(llamadas, contenido.DeepClone());
    }
}
