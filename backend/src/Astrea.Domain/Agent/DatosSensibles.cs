using System.Text.RegularExpressions;

namespace Astrea.Domain.Agent;

/// <summary>
/// Minimización de datos: antes de guardar una pregunta o enviarla al proveedor de IA se ocultan los datos personales
/// que no hacen falta para responder (DNI, CUIL, emails y teléfonos).
/// </summary>
public static partial class DatosSensibles
{
    public static string Ocultar(string texto)
    {
        var resultado = Email().Replace(texto, "[email]");
        resultado = Cuil().Replace(resultado, "[CUIL]");
        resultado = Telefono().Replace(resultado, "[teléfono]");
        return Dni().Replace(resultado, "[DNI]");
    }

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+", RegexOptions.CultureInvariant)]
    private static partial Regex Email();

    // 20-12345678-3, 20123456783.
    [GeneratedRegex(@"\b\d{2}-?\d{8}-?\d\b", RegexOptions.CultureInvariant)]
    private static partial Regex Cuil();

    // Celulares y fijos con característica: +54 9 11 1234-5678, 011 4567-8901, 15-1234-5678.
    [GeneratedRegex(@"(\+?54[\s-]?)?(9[\s-]?)?\(?0?\d{2,4}\)?[\s-]?(15[\s-]?)?\d{3,4}[\s-]\d{4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex Telefono();

    // 12345678, 12.345.678, 1.234.567 (7 u 8 cifras). No toca años ni números de resolución como 4043/09.
    [GeneratedRegex(@"\b(\d{1,2}\.\d{3}\.\d{3}|\d{7,8})\b", RegexOptions.CultureInvariant)]
    private static partial Regex Dni();
}
