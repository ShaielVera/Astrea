using Astrea.Domain.Knowledge;

namespace Astrea.Application.Abstractions.Documents;

/// <summary>Puerto de salida: saca el texto de cada página de un PDF.</summary>
public interface IExtractorTextoPdf
{
    /// <exception cref="PdfIlegibleException">El archivo está dañado, protegido con contraseña o no es un PDF.</exception>
    IReadOnlyList<PaginaTexto> Extraer(byte[] pdf);
}

/// <summary>El PDF no se pudo leer. El mensaje se muestra a la Secretaría tal cual.</summary>
public sealed class PdfIlegibleException(string mensaje, Exception? inner = null) : Exception(mensaje, inner);
