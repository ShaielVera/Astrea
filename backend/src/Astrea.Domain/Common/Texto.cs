namespace Astrea.Domain.Common;

/// <summary>Validaciones de texto compartidas por las entidades.</summary>
public static class Texto
{
    public static string Requerido(string? valor, string campo, int largoMaximo)
    {
        var limpio = valor?.Trim() ?? string.Empty;
        if (limpio.Length == 0)
        {
            throw new DomainException($"El {campo} es obligatorio.");
        }

        if (limpio.Length > largoMaximo)
        {
            throw new DomainException($"El {campo} no puede superar los {largoMaximo} caracteres.");
        }

        return limpio;
    }

    public static string? Opcional(string? valor, string campo, int largoMaximo) =>
        string.IsNullOrWhiteSpace(valor) ? null : Requerido(valor, campo, largoMaximo);
}
