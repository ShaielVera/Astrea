namespace Astrea.Api;

/// <summary>Ambientes propios de Astrea, además de Development y Production.</summary>
internal static class Ambientes
{
    /// <summary>
    /// Versión de demostración publicada en internet (Azure): como producción, pero los códigos de verificación se muestran
    /// en pantalla en lugar de mandarse por email o SMS.
    /// </summary>
    public const string Demostracion = "Demostracion";
}
