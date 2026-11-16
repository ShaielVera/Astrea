using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Astrea.Application.Abstractions.Ai;

namespace Astrea.Infrastructure.Ai;

/// <summary>
/// Modelo local y determinista, sin red, para tests y E2E. Decide con reglas simples sobre la pregunta (el texto entre
/// comillas es el "argumento"): correlatividades, mesas, objetos perdidos, mis trámites, pedir una constancia de alumno
/// regular, o pedir una herramienta prohibida ("aprobá…"). Con el resultado de la herramienta responde citándolo;
/// sin herramienta, responde con la oración más parecida del primer fragmento o se abstiene.
/// </summary>
internal sealed partial class ModeloDePrueba : IModeloLenguaje
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<TurnoLlamadas> SiguientePasoAsync(ConsultaAgente consulta, CancellationToken cancellationToken)
    {
        var indiceUsuario = consulta.Turnos.Select((turno, i) => (turno, i)).Last(par => par.turno is TurnoUsuario).i;
        var mensaje = ((TurnoUsuario)consulta.Turnos[indiceUsuario]).Texto;
        var resultados = consulta.Turnos.Skip(indiceUsuario + 1).OfType<TurnoResultados>().LastOrDefault();

        var llamada = resultados is null ? PrimerPaso(mensaje) : ConResultados(resultados);
        return Task.FromResult(new TurnoLlamadas([llamada]));
    }

    private static LlamadaHerramienta PrimerPaso(string mensaje)
    {
        var pregunta = PreguntaDe(mensaje);
        var normalizada = Normalizar(pregunta);
        var comillas = Comillas().Matches(pregunta).Select(m => m.Groups["texto"].Value).ToList();
        var primera = comillas.ElementAtOrDefault(0);

        if (normalizada.Contains("aproba mi", StringComparison.Ordinal))
        {
            return Llamar("aprobar_tramite", new { numero = primera });
        }

        if (normalizada.Contains("constancia de alumno regular", StringComparison.Ordinal))
        {
            return primera is null
                ? Responder("¿De qué carrera sos?", [], esAclaracion: true)
                : Llamar("proponer_tramite", new { tipo = "ALUMNO_REGULAR", carrera = primera, datos = new[] { new { clave = "presentarAnte", valor = comillas.ElementAtOrDefault(1) ?? "quien corresponda" } } });
        }

        if (normalizada.Contains("correlativ", StringComparison.Ordinal) && primera is not null)
        {
            return Llamar("buscar_correlatividades", new { materia = primera });
        }

        if (normalizada.Contains("mesa", StringComparison.Ordinal) && primera is not null)
        {
            return Llamar("buscar_mesas", new { materia = primera });
        }

        if (normalizada.Contains("perdi", StringComparison.Ordinal))
        {
            return Llamar("buscar_hallazgos", new { texto = primera });
        }

        if (normalizada.Contains("mis tramites", StringComparison.Ordinal))
        {
            return Llamar("consultar_mis_tramites", new { });
        }

        // Pregunta sobre normativa: responde con la oración del primer fragmento que más palabras comparte con la pregunta.
        var fragmento = Fragmentos().Match(mensaje);
        if (!fragmento.Success)
        {
            return Responder(string.Empty, [], sinEvidencia: true);
        }

        var palabras = Palabras(pregunta).ToHashSet();
        var oracion = Oraciones().Split(fragmento.Groups["contenido"].Value)
            .Select(texto => texto.Trim())
            .Where(texto => texto.Length > 0)
            .OrderByDescending(texto => Palabras(texto).Count(palabras.Contains))
            .First();
        var id = fragmento.Groups["id"].Value;
        return Responder($"Según los documentos: {oracion} [{id}]", [id]);
    }

    private static LlamadaHerramienta ConResultados(TurnoResultados resultados)
    {
        var contenido = JsonSerializer.SerializeToElement(resultados.Resultados[0].Contenido, resultados.Resultados[0].Contenido.GetType(), Json);

        if (contenido.TryGetProperty("propuesta", out var propuesta))
        {
            return Responder($"Te preparé la solicitud de {propuesta.GetProperty("tipo").GetString()}. Revisala y, si está bien, tocá \"Confirmar y enviar\".", []);
        }

        if (contenido.TryGetProperty("aclaracion", out _))
        {
            var opciones = contenido.GetProperty("opciones").EnumerateArray().Select(opcion => opcion.GetString());
            return Responder($"¿De cuál de estas materias hablás: {string.Join(" o ", opciones)}?", [], esAclaracion: true);
        }

        if (!contenido.TryGetProperty("id", out var id))
        {
            return Responder(string.Empty, [], sinEvidencia: true);
        }

        var dato = id.GetString()!;
        var detalle = resultados.Resultados[0].Nombre switch
        {
            "buscar_mesas" => contenido.GetProperty("mesas").EnumerateArray().Select(m => $"{m.GetProperty("materia").GetString()} el {m.GetProperty("fecha").GetString()} a las {m.GetProperty("hora").GetString()}").FirstOrDefault()
                ?? "no hay mesas publicadas",
            "buscar_correlatividades" => $"para cursar {Unir(contenido.GetProperty("paraCursar"))}; para rendir {Unir(contenido.GetProperty("paraRendir"))}",
            "buscar_hallazgos" => Unir(contenido.GetProperty("hallazgos"), "titulo"),
            "consultar_mis_tramites" => string.Join(", ", contenido.GetProperty("tramites").EnumerateArray().Select(t => $"{t.GetProperty("numero").GetString()} {t.GetProperty("estado").GetString()}")),
            _ => "ver el detalle",
        };
        return Responder($"Según los datos actuales de Astrea: {detalle} [{dato}].", [dato]);
    }

    private static string Unir(JsonElement lista, string? propiedad = null)
    {
        var textos = lista.EnumerateArray().Select(item => propiedad is null ? item.GetString() : item.GetProperty(propiedad).GetString()).ToList();
        return textos.Count == 0 ? "nada" : string.Join(", ", textos);
    }

    private static LlamadaHerramienta Llamar(string nombre, object argumentos) =>
        new(nombre, JsonSerializer.SerializeToElement(argumentos, argumentos.GetType(), Json));

    private static LlamadaHerramienta Responder(string texto, string[] fuentes, bool sinEvidencia = false, bool esAclaracion = false) =>
        Llamar("responder", new { respuesta = texto, fuentes, sinEvidencia, esAclaracion });

    private static string PreguntaDe(string mensaje)
    {
        var inicio = mensaje.LastIndexOf("<pregunta>", StringComparison.Ordinal);
        return inicio < 0 ? mensaje : mensaje[(inicio + "<pregunta>".Length)..].Replace("</pregunta>", string.Empty, StringComparison.Ordinal).Trim();
    }

    private static IEnumerable<string> Palabras(string texto) =>
        NoPalabra().Split(texto.ToLowerInvariant()).Where(palabra => palabra.Length > 3);

    private static string Normalizar(string texto) =>
        new(texto.ToLowerInvariant().Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());

    [GeneratedRegex("[\"“«](?<texto>[^\"”»]+)[\"”»]", RegexOptions.CultureInvariant)]
    private static partial Regex Comillas();

    [GeneratedRegex("<fragmento id=\"(?<id>F\\d+)\"[^>]*>\\n(?<contenido>.*?)\\n</fragmento>", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex Fragmentos();

    [GeneratedRegex(@"(?<=[.!?])\s+|\n+", RegexOptions.CultureInvariant)]
    private static partial Regex Oraciones();

    [GeneratedRegex(@"[^\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex NoPalabra();
}
