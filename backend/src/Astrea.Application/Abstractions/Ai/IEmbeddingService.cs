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

    public ConfiguracionAsistente Asistente { get; init; } = new();
}

/// <summary>Política de recuperación del asistente (sección "Ia:Asistente").</summary>
public sealed class ConfiguracionAsistente
{
    /// <summary>
    /// Similitud coseno mínima para considerar que un fragmento habla de la pregunta. Por debajo no se consulta al modelo
    /// y el asistente reconoce que no tiene información. Depende del proveedor de embeddings.
    /// </summary>
    public double SimilitudMinima { get; init; } = 0.5;

    /// <summary>Cuántos fragmentos como máximo se le pasan al modelo.</summary>
    public int FragmentosMaximos { get; init; } = 6;

    /// <summary>Tope de texto de las evidencias (controla costo y latencia).</summary>
    public int CaracteresMaximosContexto { get; init; } = 12_000;
}

public sealed class ConfiguracionGemini
{
    public string? ApiKey { get; init; }

    public string ModeloEmbeddings { get; init; } = "gemini-embedding-001";

    /// <summary>
    /// Modelo que redacta las respuestas del asistente. Flash-Lite alcanza para redactar a partir de fragmentos dados
    /// y responde en ~1 s (los Flash completos tardaban 10-20 s en el plan gratuito).
    /// </summary>
    public string ModeloChat { get; init; } = "gemini-3.5-flash-lite";

    /// <summary>Cuánto "piensa" el modelo antes de responder: más bajo es más rápido. Vacío usa el del modelo.</summary>
    public string? NivelRazonamiento { get; init; } = "low";

    public string UrlBase { get; init; } = "https://generativelanguage.googleapis.com/v1beta/";
}
