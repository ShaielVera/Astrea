using System.Text.RegularExpressions;
using Astrea.Domain.Common;

namespace Astrea.Domain.Academic;

/// <summary>Carrera del instituto (ej. TSDS). Tiene uno o más planes de estudio.</summary>
public sealed partial class Carrera : Entity
{
    public const int DuracionMaximaAnios = 6;

    private Carrera()
    {
    }

    private Carrera(Guid id, string codigo, string nombre, int duracionAnios, DateTimeOffset ahora)
        : base(id)
    {
        Codigo = codigo;
        Nombre = nombre;
        DuracionAnios = duracionAnios;
        Activa = true;
        CreatedAt = ahora;
        UpdatedAt = ahora;
    }

    public string Codigo { get; private set; } = null!;

    public string Nombre { get; private set; } = null!;

    public int DuracionAnios { get; private set; }

    public bool Activa { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Carrera Crear(Guid id, string codigo, string nombre, int duracionAnios, DateTimeOffset ahora) =>
        new(id, NormalizarCodigo(codigo), Texto.Requerido(nombre, "nombre de la carrera", 200), ValidarDuracion(duracionAnios), ahora);

    public void Actualizar(string nombre, int duracionAnios, bool activa, DateTimeOffset ahora)
    {
        Nombre = Texto.Requerido(nombre, "nombre de la carrera", 200);
        DuracionAnios = ValidarDuracion(duracionAnios);
        Activa = activa;
        UpdatedAt = ahora;
    }

    /// <summary>Códigos en mayúsculas, sin puntos ni espacios: "T.S.D.S." → "TSDS".</summary>
    public static string NormalizarCodigo(string codigo)
    {
        var normalizado = (codigo ?? string.Empty).Trim().Replace(".", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (!FormatoCodigo().IsMatch(normalizado))
        {
            throw new DomainException("El código de la carrera debe tener entre 2 y 20 letras, números o guiones.");
        }

        return normalizado;
    }

    private static int ValidarDuracion(int duracionAnios) =>
        duracionAnios is >= 1 and <= DuracionMaximaAnios
            ? duracionAnios
            : throw new DomainException($"La duración de la carrera debe estar entre 1 y {DuracionMaximaAnios} años.");

    [GeneratedRegex("^[A-Z0-9][A-Z0-9-]{1,19}$")]
    private static partial Regex FormatoCodigo();
}
