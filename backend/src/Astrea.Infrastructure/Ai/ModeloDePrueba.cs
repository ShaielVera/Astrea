using System.Text.RegularExpressions;
using Astrea.Application.Abstractions.Ai;

namespace Astrea.Infrastructure.Ai;

/// <summary>
/// Modelo de lenguaje local y determinista, sin red, para tests y E2E: responde con la oración de la primera evidencia
/// que más palabras comparte con la pregunta y la cita. Sin evidencias, se abstiene.
/// </summary>
internal sealed partial class ModeloDePrueba : IModeloLenguaje
{
    public Task<RespuestaModelo> ResponderAsync(ConsultaModelo consulta, CancellationToken cancellationToken)
    {
        if (consulta.Evidencias.Count == 0)
        {
            return Task.FromResult(new RespuestaModelo(string.Empty, [], SinEvidencia: true));
        }

        var evidencia = consulta.Evidencias[0];
        var pregunta = Palabras(PreguntaDe(consulta.Mensaje)).ToHashSet();
        var oracion = Oraciones().Split(evidencia.Contenido)
            .Select(oracion => oracion.Trim())
            .Where(oracion => oracion.Length > 0)
            .OrderByDescending(oracion => Palabras(oracion).Count(pregunta.Contains))
            .First();

        return Task.FromResult(new RespuestaModelo($"Según los documentos: {oracion} [{evidencia.Id}]", [evidencia.Id], SinEvidencia: false));
    }

    private static string PreguntaDe(string mensaje)
    {
        var inicio = mensaje.LastIndexOf("<pregunta>", StringComparison.Ordinal);
        return inicio < 0 ? mensaje : mensaje[(inicio + "<pregunta>".Length)..].Replace("</pregunta>", string.Empty, StringComparison.Ordinal);
    }

    private static IEnumerable<string> Palabras(string texto) =>
        NoPalabra().Split(texto.ToLowerInvariant()).Where(palabra => palabra.Length > 3);

    [GeneratedRegex(@"(?<=[.!?])\s+|\n+", RegexOptions.CultureInvariant)]
    private static partial Regex Oraciones();

    [GeneratedRegex(@"[^\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex NoPalabra();
}
