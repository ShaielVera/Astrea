using Astrea.Domain.Common;

namespace Astrea.Domain.Knowledge;

public enum TipoDocumentoRag
{
    Reglamento = 1,
    PlanDeEstudio = 2,
    Cronograma = 3,
    Comunicado = 4,
    Otro = 5,
}

/// <summary>
/// Pendiente → Procesando → Indexado (o Error, que se puede reintentar).
/// Un Indexado pasa a Reemplazado cuando se indexa una versión nueva, o a Retirado si la Secretaría lo saca de las consultas.
/// </summary>
public enum EstadoIndexacion
{
    Pendiente = 1,
    Procesando = 2,
    Indexado = 3,
    Error = 4,
    Reemplazado = 5,
    Retirado = 6,
}

/// <summary>
/// Documento institucional (reglamento, plan, cronograma…) cargado para que el asistente lo consulte.
/// Cada carga es una versión: todas las versiones de un mismo documento comparten <see cref="SerieId"/>.
/// La versión anterior sigue siendo la vigente hasta que la nueva termina de indexarse sin errores.
/// </summary>
public sealed class DocumentoRag : Entity
{
    public const int TamanioMaximoBytes = 20 * 1024 * 1024;

    public const int PaginasMaximas = 500;

    private DocumentoRag()
    {
    }

    private DocumentoRag(Guid id, Guid serieId, int version, DatosDocumentoRag datos, string storagePath, string hashSha256, Guid cargadoPorUsuarioId, DateTimeOffset ahora)
        : base(id)
    {
        if (cargadoPorUsuarioId == Guid.Empty)
        {
            throw new DomainException("El documento debe registrar quién lo cargó.");
        }

        if (datos.FechaDocumento is { } fecha && fecha > DateOnly.FromDateTime(ahora.UtcDateTime).AddDays(1))
        {
            throw new DomainException("La fecha del documento no puede ser futura.");
        }

        if (!Enum.IsDefined(datos.Tipo))
        {
            throw new DomainException("El tipo de documento no es válido.");
        }

        if (string.IsNullOrWhiteSpace(hashSha256) || hashSha256.Length != 64)
        {
            throw new DomainException("El hash del archivo no es válido.");
        }

        SerieId = serieId;
        Version = version;
        Titulo = Texto.Requerido(datos.Titulo, "título", 150);
        Tipo = datos.Tipo;
        Fuente = Texto.Opcional(datos.Fuente, "texto de la fuente", 200);
        FechaDocumento = datos.FechaDocumento;
        StoragePath = Texto.Requerido(storagePath, "ruta del archivo", 300);
        HashSha256 = hashSha256.ToLowerInvariant();
        CargadoPorUsuarioId = cargadoPorUsuarioId;
        FechaCarga = ahora;
        Estado = EstadoIndexacion.Pendiente;
    }

    /// <summary>Agrupa las versiones de un mismo documento.</summary>
    public Guid SerieId { get; private set; }

    public int Version { get; private set; }

    public string Titulo { get; private set; } = null!;

    public TipoDocumentoRag Tipo { get; private set; }

    /// <summary>De dónde sale (ej. "Resolución 1234/2024", "Secretaría"). Se cita en las respuestas del asistente.</summary>
    public string? Fuente { get; private set; }

    public DateOnly? FechaDocumento { get; private set; }

    public string StoragePath { get; private set; } = null!;

    public string HashSha256 { get; private set; } = null!;

    public Guid CargadoPorUsuarioId { get; private set; }

    public DateTimeOffset FechaCarga { get; private set; }

    public EstadoIndexacion Estado { get; private set; }

    public string? ErrorProcesamiento { get; private set; }

    public int Intentos { get; private set; }

    public int? CantidadPaginas { get; private set; }

    public int? CantidadFragmentos { get; private set; }

    public DateTimeOffset? FechaIndexacion { get; private set; }

    public Guid? RetiradoPorUsuarioId { get; private set; }

    /// <summary>Solo los documentos indexados (versión vigente) se usan para responder.</summary>
    public bool EsConsultable => Estado == EstadoIndexacion.Indexado;

    /// <summary>Controla que el archivo sea un PDF (por su contenido, no por la extensión) y no supere el tamaño máximo.</summary>
    public static void ValidarArchivo(byte[] contenido)
    {
        if (contenido is not { Length: > 0 })
        {
            throw new DomainException("El archivo está vacío.");
        }

        if (contenido.Length > TamanioMaximoBytes)
        {
            throw new DomainException($"El PDF no puede superar los {TamanioMaximoBytes / (1024 * 1024)} MB.");
        }

        // La firma "%PDF-" va al principio (se toleran algunos bytes basura antes, como hacen los lectores).
        if (contenido.AsSpan(0, Math.Min(1024, contenido.Length)).IndexOf("%PDF-"u8) >= 0)
        {
            return;
        }

        throw new DomainException("El archivo no es un PDF.");
    }

    public static DocumentoRag Cargar(Guid id, DatosDocumentoRag datos, string storagePath, string hashSha256, Guid cargadoPorUsuarioId, DateTimeOffset ahora) =>
        new(id, Guid.CreateVersion7(), 1, datos, storagePath, hashSha256, cargadoPorUsuarioId, ahora);

    /// <summary>Nueva versión del mismo documento. La anterior no cambia hasta que esta quede indexada.</summary>
    public DocumentoRag NuevaVersion(Guid id, int ultimaVersion, DatosDocumentoRag datos, string storagePath, string hashSha256, Guid cargadoPorUsuarioId, DateTimeOffset ahora)
    {
        if (string.Equals(HashSha256, hashSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Ese archivo es idéntico a la versión que ya está cargada.");
        }

        return new DocumentoRag(id, SerieId, Math.Max(ultimaVersion, Version) + 1, datos, storagePath, hashSha256, cargadoPorUsuarioId, ahora);
    }

    public void IniciarProcesamiento()
    {
        if (Estado != EstadoIndexacion.Pendiente)
        {
            throw new DomainException("Solo se procesa un documento pendiente.");
        }

        Estado = EstadoIndexacion.Procesando;
        Intentos++;
        ErrorProcesamiento = null;
    }

    public void MarcarIndexado(int cantidadPaginas, int cantidadFragmentos, DateTimeOffset ahora)
    {
        ValidarProcesando();
        if (cantidadFragmentos <= 0)
        {
            throw new DomainException("Un documento indexado debe tener al menos un fragmento.");
        }

        Estado = EstadoIndexacion.Indexado;
        CantidadPaginas = cantidadPaginas;
        CantidadFragmentos = cantidadFragmentos;
        FechaIndexacion = ahora;
        ErrorProcesamiento = null;
    }

    public void MarcarError(string mensaje)
    {
        ValidarProcesando();
        Estado = EstadoIndexacion.Error;
        var limpio = string.IsNullOrWhiteSpace(mensaje) ? "Error desconocido al procesar el documento." : mensaje.Trim();
        ErrorProcesamiento = limpio.Length > 500 ? limpio[..500] : limpio;
    }

    /// <summary>Si el proceso se cortó (ej. se reinició el servidor) mientras estaba procesando, vuelve a la cola.</summary>
    public void DevolverALaCola()
    {
        if (Estado == EstadoIndexacion.Procesando)
        {
            Estado = EstadoIndexacion.Pendiente;
        }
    }

    /// <summary>Vuelve a procesar: después de un error, o para regenerar los fragmentos de uno indexado.</summary>
    public void Reindexar()
    {
        if (Estado is not (EstadoIndexacion.Error or EstadoIndexacion.Indexado))
        {
            throw new DomainException(Estado switch
            {
                EstadoIndexacion.Pendiente or EstadoIndexacion.Procesando => "El documento ya está en proceso.",
                EstadoIndexacion.Reemplazado => "Esta versión ya fue reemplazada por una más nueva.",
                _ => "El documento fue retirado: cargalo de nuevo si querés volver a usarlo.",
            });
        }

        Estado = EstadoIndexacion.Pendiente;
        ErrorProcesamiento = null;
    }

    /// <summary>Se indexó una versión más nueva del mismo documento.</summary>
    public void MarcarReemplazado()
    {
        if (Estado == EstadoIndexacion.Indexado)
        {
            Estado = EstadoIndexacion.Reemplazado;
        }
    }

    /// <summary>La Secretaría lo saca de las consultas (ej. normativa derogada). Queda registrado quién.</summary>
    public void Retirar(Guid administradorId)
    {
        if (Estado is EstadoIndexacion.Retirado or EstadoIndexacion.Procesando)
        {
            throw new DomainException(Estado == EstadoIndexacion.Retirado ? "El documento ya fue retirado." : "Esperá a que termine de procesarse.");
        }

        Estado = EstadoIndexacion.Retirado;
        RetiradoPorUsuarioId = administradorId;
    }

    private void ValidarProcesando()
    {
        if (Estado != EstadoIndexacion.Procesando)
        {
            throw new DomainException("El documento no se está procesando.");
        }
    }
}

public sealed record DatosDocumentoRag(string Titulo, TipoDocumentoRag Tipo, string? Fuente, DateOnly? FechaDocumento);

/// <summary>
/// Pedazo de un documento con su embedding. Lleva copiados los metadatos que el asistente necesita para citar
/// (título, versión, sección, páginas) sin tener que unir con otras tablas.
/// </summary>
public sealed class FragmentoRag : Entity
{
    /// <summary>Dimensiones del vector (gemini-embedding-001 reducido a 768: entra en un índice HNSW de pgvector).</summary>
    public const int DimensionesEmbedding = 768;

    private FragmentoRag()
    {
    }

    public FragmentoRag(Guid id, Guid documentoRagId, int numeroFragmento, string contenido, float[] embedding, MetadataFragmento metadata, int paginaDesde, int paginaHasta)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(contenido))
        {
            throw new DomainException("El fragmento no puede estar vacío.");
        }

        if (embedding?.Length != DimensionesEmbedding)
        {
            throw new DomainException($"El embedding debe tener {DimensionesEmbedding} dimensiones.");
        }

        if (paginaDesde < 1 || paginaHasta < paginaDesde)
        {
            throw new DomainException("Las páginas del fragmento no son válidas.");
        }

        DocumentoRagId = documentoRagId;
        NumeroFragmento = numeroFragmento;
        Contenido = contenido;
        Embedding = embedding;
        Metadata = metadata;
        PaginaDesde = paginaDesde;
        PaginaHasta = paginaHasta;
    }

    public Guid DocumentoRagId { get; private set; }

    public int NumeroFragmento { get; private set; }

    public string Contenido { get; private set; } = null!;

    public float[] Embedding { get; private set; } = null!;

    public MetadataFragmento Metadata { get; private set; } = null!;

    public int PaginaDesde { get; private set; }

    public int PaginaHasta { get; private set; }
}

/// <summary>Lo que se cita junto al fragmento.</summary>
public sealed record MetadataFragmento(string Titulo, TipoDocumentoRag Tipo, int Version, string? Seccion, string? Fuente, DateOnly? FechaDocumento);
