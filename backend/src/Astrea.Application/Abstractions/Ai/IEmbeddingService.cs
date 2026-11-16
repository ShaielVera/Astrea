namespace Astrea.Application.Abstractions.Ai;

/// <summary>Para qué se genera el vector: algunos modelos optimizan distinto el texto guardado y la pregunta.</summary>
public enum PropositoEmbedding
{
    Documento,
    Consulta,
}

/// <summary>Puerto de salida: convierte textos en vectores (embeddings) para la búsqueda semántica.</summary>
public interface IEmbeddingService
{
    /// <summary>Devuelve un vector normalizado por texto, en el mismo orden, de <see cref="Domain.Knowledge.FragmentoRag.DimensionesEmbedding"/> dimensiones.</summary>
    /// <exception cref="EmbeddingNoDisponibleException">El proveedor no está configurado o no respondió.</exception>
    Task<IReadOnlyList<float[]>> GenerarAsync(IReadOnlyList<string> textos, PropositoEmbedding proposito, CancellationToken cancellationToken);
}

/// <summary>El proveedor de embeddings no se pudo usar. El mensaje se muestra a la Secretaría tal cual.</summary>
public sealed class EmbeddingNoDisponibleException(string mensaje, Exception? inner = null) : Exception(mensaje, inner);

/// <summary>Configuración de la IA (sección "Ia"). La API key va en user-secrets o variables de entorno, nunca en el repositorio.</summary>
public sealed class ConfiguracionIa
{
    public const string Seccion = "Ia";

    public const string ProveedorGemini = "Gemini";

    /// <summary>Embeddings locales y deterministas, sin red: para tests y E2E.</summary>
    public const string ProveedorPrueba = "Prueba";

    public string Proveedor { get; init; } = ProveedorGemini;

    public ConfiguracionGemini Gemini { get; init; } = new();
}

public sealed class ConfiguracionGemini
{
    public string? ApiKey { get; init; }

    public string ModeloEmbeddings { get; init; } = "gemini-embedding-001";

    public string UrlBase { get; init; } = "https://generativelanguage.googleapis.com/v1beta/";
}
