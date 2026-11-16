using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Astrea.Application.Abstractions.Ai;
using Astrea.Domain.Knowledge;

namespace Astrea.Infrastructure.Ai;

/// <summary>
/// Embeddings locales y deterministas, sin red ni costo, para tests y E2E: cada palabra suma en una posición del
/// vector según su hash. Textos con palabras en común quedan cerca; no entiende sinónimos (no es un modelo real).
/// </summary>
internal sealed class EmbeddingsDePrueba : IEmbeddingService
{
    public Task<IReadOnlyList<float[]>> GenerarAsync(IReadOnlyList<string> textos, PropositoEmbedding proposito, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<float[]>>(textos.Select(Vector).ToList());

    internal static float[] Vector(string texto)
    {
        var vector = new float[FragmentoRag.DimensionesEmbedding];
        foreach (var palabra in Palabras(texto))
        {
            var hash = Fnv1a(palabra);
            vector[hash % (uint)vector.Length] += (hash & 0x8000_0000) == 0 ? 1 : -1;
        }

        if (vector.All(valor => valor == 0))
        {
            vector[0] = 1;
        }

        return Vectores.Normalizar(vector);
    }

    private static IEnumerable<string> Palabras(string texto)
    {
        var sinTildes = new string(texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return Regex.Split(sinTildes.ToLowerInvariant(), @"[^\p{L}\p{N}]+")
            .Where(palabra => palabra.Length >= 3);
    }

    private static uint Fnv1a(string texto)
    {
        var hash = 2166136261;
        foreach (var c in texto)
        {
            hash = (hash ^ c) * 16777619;
        }

        return hash;
    }
}

