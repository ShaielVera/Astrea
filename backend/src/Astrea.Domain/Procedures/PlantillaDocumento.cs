using System.Text.RegularExpressions;
using Astrea.Domain.Common;

namespace Astrea.Domain.Procedures;

/// <summary>
/// Texto con el que se emite el PDF de un tipo de trámite. Es inmutable: corregirla crea una versión nueva,
/// y cada documento emitido recuerda con qué versión se generó.
/// <para>
/// El cuerpo usa marcadores <c>{{clave}}</c>; un marcador con valor por defecto se escribe <c>{{clave|texto}}</c>
/// y se usa cuando el dato opcional no se completó.
/// </para>
/// </summary>
public sealed partial class PlantillaDocumento : Entity
{
    /// <summary>Datos del titular y del trámite disponibles en toda plantilla (además de los campos del formulario).</summary>
    public static readonly IReadOnlyList<string> MarcadoresComunes =
        ["nombreCompleto", "nombre", "apellido", "dni", "legajo", "numeroTramite", "fechaEmision"];

    private PlantillaDocumento()
    {
    }

    private PlantillaDocumento(Guid id, Guid tramiteTipoId, int version, string titulo, string cuerpo, DateTimeOffset creadaAt)
        : base(id)
    {
        TramiteTipoId = tramiteTipoId;
        Version = version;
        Titulo = titulo;
        Cuerpo = cuerpo;
        CreadaAt = creadaAt;
    }

    public Guid TramiteTipoId { get; private set; }

    public int Version { get; private set; }

    public string Titulo { get; private set; } = null!;

    public string Cuerpo { get; private set; } = null!;

    public DateTimeOffset CreadaAt { get; private set; }

    /// <summary>Crea una versión validando que solo use marcadores conocidos para ese tipo de trámite.</summary>
    public static PlantillaDocumento Crear(Guid id, TramiteTipo tipo, int version, string titulo, string cuerpo, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        if (version < 1)
        {
            throw new DomainException("La versión de la plantilla empieza en 1.");
        }

        var texto = Texto.Requerido(cuerpo, "texto de la plantilla", 4000);
        var permitidos = MarcadoresPermitidos(tipo);
        var desconocidos = Marcador().Matches(texto)
            .Select(coincidencia => coincidencia.Groups["clave"].Value)
            .Where(clave => !permitidos.Contains(clave))
            .Distinct()
            .ToList();
        if (desconocidos.Count > 0)
        {
            throw new DomainException(
                $"La plantilla usa datos que no existen: {string.Join(", ", desconocidos.Select(clave => $"{{{{{clave}}}}}"))}. " +
                $"Disponibles: {string.Join(", ", permitidos.Select(clave => $"{{{{{clave}}}}}"))}.");
        }

        return new PlantillaDocumento(id, tipo.Id, version, Texto.Requerido(titulo, "título del documento", 120), texto, ahora);
    }

    public static IReadOnlyList<string> MarcadoresPermitidos(TramiteTipo tipo) =>
        MarcadoresComunes.Concat(tipo.Campos.Select(campo => campo.Clave)).ToList();

    /// <summary>Reemplaza los marcadores. Un dato faltante usa el valor por defecto del marcador, o queda vacío.</summary>
    public string Renderizar(IReadOnlyDictionary<string, string> valores) =>
        Marcador().Replace(Cuerpo, coincidencia =>
            valores.TryGetValue(coincidencia.Groups["clave"].Value, out var valor) && !string.IsNullOrWhiteSpace(valor)
                ? valor
                : coincidencia.Groups["defecto"].Value);

    [GeneratedRegex(@"\{\{(?<clave>[A-Za-z][A-Za-z0-9]*)(\|(?<defecto>[^}]*))?\}\}")]
    private static partial Regex Marcador();
}
