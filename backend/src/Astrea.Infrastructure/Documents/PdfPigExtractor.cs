using Astrea.Application.Abstractions.Documents;
using Astrea.Domain.Knowledge;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace Astrea.Infrastructure.Documents;

/// <summary>Extrae el texto de cada página con PdfPig, en orden de lectura (respeta los saltos de línea).</summary>
internal sealed class PdfPigExtractor : IExtractorTextoPdf
{
    public IReadOnlyList<PaginaTexto> Extraer(byte[] pdf)
    {
        try
        {
            using var documento = PdfDocument.Open(pdf);
            return documento.GetPages()
                .Select(pagina => new PaginaTexto(pagina.Number, ContentOrderTextExtractor.GetText(pagina)))
                .ToList();
        }
        catch (PdfDocumentEncryptedException ex)
        {
            throw new PdfIlegibleException("El PDF está protegido con contraseña. Cargá una versión sin protección.", ex);
        }
        catch (Exception ex) when (ex is not PdfIlegibleException and not OutOfMemoryException)
        {
            throw new PdfIlegibleException("No se pudo leer el PDF: puede estar dañado. Probá exportarlo de nuevo.", ex);
        }
    }
}
