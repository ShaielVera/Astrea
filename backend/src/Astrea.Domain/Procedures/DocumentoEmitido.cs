using System.Security.Cryptography;
using Astrea.Domain.Common;

namespace Astrea.Domain.Procedures;

public enum EstadoDocumento
{
    Vigente = 1,
    Anulado = 2,
}

/// <summary>
/// PDF oficial emitido al aprobar un trámite. El archivo es privado; su autenticidad se verifica con el
/// <see cref="CodigoValidacion"/> (el del QR), que muestra solo datos mínimos.
/// </summary>
public sealed class DocumentoEmitido : Entity
{
    /// <summary>Alfabeto Crockford base 32: sin I, L, O ni U para que no se confundan al tipearlo.</summary>
    private const string Alfabeto = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private DocumentoEmitido()
    {
    }

    private DocumentoEmitido(
        Guid id,
        Guid tramiteId,
        Guid usuarioId,
        Guid plantillaId,
        int versionPlantilla,
        string tipoDocumento,
        string codigoValidacion,
        string storagePath,
        string hashSha256,
        string? dniUltimosDigitos,
        DateTimeOffset fechaEmision)
        : base(id)
    {
        TramiteId = tramiteId;
        UsuarioId = usuarioId;
        PlantillaId = plantillaId;
        VersionPlantilla = versionPlantilla;
        TipoDocumento = tipoDocumento;
        CodigoValidacion = codigoValidacion;
        StoragePath = storagePath;
        HashSha256 = hashSha256;
        DniUltimosDigitos = dniUltimosDigitos;
        FechaEmision = fechaEmision;
        Estado = EstadoDocumento.Vigente;
    }

    public Guid TramiteId { get; private set; }

    public Guid UsuarioId { get; private set; }

    public Guid PlantillaId { get; private set; }

    public int VersionPlantilla { get; private set; }

    /// <summary>Nombre del documento al emitirlo (ej. "Constancia de alumno regular").</summary>
    public string TipoDocumento { get; private set; } = null!;

    /// <summary>Código único del QR, en formato XXXX-XXXX-XXXX.</summary>
    public string CodigoValidacion { get; private set; } = null!;

    public string StoragePath { get; private set; } = null!;

    /// <summary>Huella SHA-256 del PDF: permite comprobar que el archivo no fue modificado.</summary>
    public string HashSha256 { get; private set; } = null!;

    /// <summary>Solo los últimos 3 dígitos del DNI del titular, para la validación pública.</summary>
    public string? DniUltimosDigitos { get; private set; }

    public DateTimeOffset FechaEmision { get; private set; }

    public EstadoDocumento Estado { get; private set; }

    public string? MotivoAnulacion { get; private set; }

    public Guid? AnuladoPorUsuarioId { get; private set; }

    public DateTimeOffset? AnuladoAt { get; private set; }

    public static DocumentoEmitido Emitir(
        Guid id,
        Tramite tramite,
        PlantillaDocumento plantilla,
        string tipoDocumento,
        string codigoValidacion,
        string storagePath,
        byte[] contenido,
        string? dni,
        DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(tramite);
        ArgumentNullException.ThrowIfNull(plantilla);
        if (contenido is not { Length: > 0 })
        {
            throw new DomainException("El documento no tiene contenido.");
        }

        if (plantilla.TramiteTipoId != tramite.TramiteTipoId)
        {
            throw new DomainException("La plantilla no corresponde al tipo de trámite.");
        }

        return new DocumentoEmitido(
            id,
            tramite.Id,
            tramite.UsuarioId,
            plantilla.Id,
            plantilla.Version,
            tipoDocumento,
            NormalizarCodigo(codigoValidacion) ?? throw new DomainException("El código de validación no tiene un formato válido."),
            Texto.Requerido(storagePath, "ruta del archivo", 300),
            CalcularHash(contenido),
            dni is { Length: >= 3 } ? dni[^3..] : null,
            ahora);
    }

    /// <summary>Anula el documento (ej. emitido por error). La validación pública pasa a mostrarlo como anulado.</summary>
    public void Anular(Guid administradorId, string motivo, DateTimeOffset ahora)
    {
        if (Estado == EstadoDocumento.Anulado)
        {
            throw new DomainException("El documento ya está anulado.");
        }

        MotivoAnulacion = Texto.Requerido(motivo, "motivo de la anulación", 300);
        AnuladoPorUsuarioId = administradorId;
        AnuladoAt = ahora;
        Estado = EstadoDocumento.Anulado;
    }

    public static string CalcularHash(byte[] contenido) => Convert.ToHexStringLower(SHA256.HashData(contenido));

    /// <summary>Código aleatorio de 12 caracteres (60 bits): imposible de adivinar por prueba y error.</summary>
    public static string GenerarCodigo()
    {
        var caracteres = RandomNumberGenerator.GetItems<char>(Alfabeto, 12);
        return $"{new string(caracteres, 0, 4)}-{new string(caracteres, 4, 4)}-{new string(caracteres, 8, 4)}";
    }

    /// <summary>
    /// Normaliza un código tipeado por una persona: mayúsculas, sin espacios ni guiones, y confusiones comunes
    /// corregidas (O → 0, I/L → 1). Devuelve null si no tiene el formato.
    /// </summary>
    public static string? NormalizarCodigo(string? codigo)
    {
        var limpio = new string((codigo ?? string.Empty).ToUpperInvariant()
            .Where(c => c is not ('-' or ' '))
            .Select(c => c switch { 'O' => '0', 'I' or 'L' => '1', _ => c })
            .ToArray());

        if (limpio.Length != 12 || limpio.Any(c => !Alfabeto.Contains(c)))
        {
            return null;
        }

        return $"{limpio[..4]}-{limpio[4..8]}-{limpio[8..]}";
    }
}
