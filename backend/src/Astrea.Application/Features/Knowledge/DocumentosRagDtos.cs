using Astrea.Application.Contracts.Results;
using Astrea.Domain.Knowledge;

namespace Astrea.Application.Features.Knowledge;

public sealed record DocumentoRagDto(
    Guid Id,
    Guid SerieId,
    int Version,
    string Titulo,
    TipoDocumentoRag Tipo,
    string? Fuente,
    DateOnly? FechaDocumento,
    DateTimeOffset FechaCarga,
    string CargadoPor,
    EstadoIndexacion Estado,
    string? ErrorProcesamiento,
    int Intentos,
    int? CantidadPaginas,
    int? CantidadFragmentos,
    DateTimeOffset? FechaIndexacion,
    Guid? CarreraId = null);

/// <summary>Un documento con todas sus versiones (la primera es la más nueva).</summary>
public sealed record DetalleDocumentoRagDto(DocumentoRagDto Documento, IReadOnlyList<DocumentoRagDto> Versiones);

public sealed record FragmentoDto(int NumeroFragmento, string Contenido, string? Seccion, int PaginaDesde, int PaginaHasta);

/// <summary>Resultado de la búsqueda semántica (lo usa el asistente para responder con fuentes).</summary>
public sealed record FragmentoEncontrado(
    Guid DocumentoId,
    int NumeroFragmento,
    string Contenido,
    MetadataFragmento Metadata,
    int PaginaDesde,
    int PaginaHasta,
    double Similitud);

/// <summary>Filtro del listado. <see cref="Texto"/> busca en el título, sin distinguir tildes.</summary>
public sealed record FiltroDocumentosRag(IReadOnlyCollection<EstadoIndexacion>? Estados = null, string? Texto = null, TipoDocumentoRag? Tipo = null);

/// <param name="NuevaVersionDe">Id de cualquier versión del documento que se reemplaza; null si es un documento nuevo.</param>
/// <param name="CarreraId">Solo para los horarios: la carrera a la que pertenecen.</param>
public sealed record CargarDocumentoRagCommand(
    string Titulo,
    TipoDocumentoRag Tipo,
    string? Fuente,
    DateOnly? FechaDocumento,
    Guid? NuevaVersionDe,
    Guid? CarreraId = null);

/// <summary>Cómo terminó el procesamiento de un documento (para el registro del servidor).</summary>
public sealed record ResultadoIngesta(Guid DocumentoId, string Titulo, bool Indexado, int Fragmentos, string? Error, Exception? Excepcion);

public static class ErroresDocumentosRag
{
    public const int LargoMaximoTexto = 100;

    public static readonly Error NoEncontrado =
        Error.NoEncontrado("documento_rag_no_encontrado", "El documento no existe.");

    public static readonly Error CarreraNoEncontrada =
        Error.Validacion("carrera_no_encontrada", "La carrera elegida no existe.");

    public static readonly Error ArchivoNoEncontrado =
        Error.NoEncontrado("documento_rag_sin_archivo", "No se encontró el archivo del documento.");

    public static readonly Error HayVersionMasNueva =
        Error.Conflicto("documento_rag_version_mas_nueva", "Hay una versión más nueva de este documento: reindexá esa.");

    public static readonly Error TextoDemasiadoLargo =
        Error.Validacion("texto_demasiado_largo", $"La búsqueda no puede superar los {LargoMaximoTexto} caracteres.");

    public static Error Duplicado(DocumentoRag existente) =>
        Error.Conflicto("documento_rag_duplicado", $"Ese archivo ya está cargado como \"{existente.Titulo}\" (versión {existente.Version}).");
}
