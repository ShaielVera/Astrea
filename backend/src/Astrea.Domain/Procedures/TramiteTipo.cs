using System.Globalization;
using Astrea.Domain.Common;

namespace Astrea.Domain.Procedures;

/// <summary>Tipo de dato de un campo del formulario de un trámite.</summary>
public enum TipoCampo
{
    Texto = 1,
    TextoLargo = 2,

    /// <summary>Fecha en formato AAAA-MM-DD.</summary>
    Fecha = 3,

    /// <summary>Id de una carrera del catálogo académico (la aplicación verifica que exista).</summary>
    Carrera = 4,
}

/// <summary>Campo variable que pide un tipo de trámite. Es el "esquema" con el que se validan los datos de la solicitud.</summary>
/// <param name="SinFechasFuturas">Para las fechas de algo que ya pasó (ej. el día del examen rendido): no acepta días posteriores a hoy.</param>
public sealed record CampoTramite(string Clave, string Etiqueta, TipoCampo Tipo, bool Obligatorio, int LargoMaximo = 120, string? Ayuda = null, bool SinFechasFuturas = false);

/// <summary>
/// Trámite que un estudiante puede pedir (ej. constancia de alumno regular), con el formulario que lo acompaña.
/// El catálogo lo define el instituto; los estudiantes solo eligen entre los tipos activos.
/// </summary>
public sealed class TramiteTipo : Entity
{
    public const int LargoMaximoTextoLargo = 1000;

    private TramiteTipo()
    {
    }

    private TramiteTipo(Guid id, string codigo, string nombre, string descripcion, IReadOnlyList<CampoTramite> campos)
        : base(id)
    {
        Codigo = codigo;
        Nombre = nombre;
        Descripcion = descripcion;
        Campos = campos;
        Activo = true;
    }

    public string Codigo { get; private set; } = null!;

    public string Nombre { get; private set; } = null!;

    public string Descripcion { get; private set; } = null!;

    /// <summary>Por ahora ningún tipo pide adjuntos: la carga de archivos llega con el almacenamiento (Slice 7).</summary>
    public bool RequiereAdjunto { get; private set; }

    public IReadOnlyList<CampoTramite> Campos { get; private set; } = [];

    public bool Activo { get; private set; }

    public static TramiteTipo Crear(Guid id, string codigo, string nombre, string descripcion, IReadOnlyList<CampoTramite> campos)
    {
        var claves = campos.Select(campo => campo.Clave).ToList();
        if (claves.Count != claves.Distinct(StringComparer.Ordinal).Count())
        {
            throw new DomainException("Un tipo de trámite no puede repetir campos.");
        }

        return new TramiteTipo(
            id,
            Texto.Requerido(codigo, "código del tipo de trámite", 40),
            Texto.Requerido(nombre, "nombre del tipo de trámite", 120),
            Texto.Requerido(descripcion, "texto de la descripción", 500),
            campos);
    }

    public void CambiarActivo(bool activo) => Activo = activo;

    /// <summary>
    /// Valida los datos de una solicitud contra los campos del tipo y los devuelve normalizados (sin espacios
    /// sobrantes y sin campos opcionales vacíos). Rechaza campos que el tipo no pide.
    /// </summary>
    /// <param name="hoy">El día de hoy en el instituto, para los campos que no aceptan fechas futuras.</param>
    public IReadOnlyDictionary<string, string> ValidarDatos(IReadOnlyDictionary<string, string?>? datos, DateOnly hoy)
    {
        datos ??= new Dictionary<string, string?>();

        var desconocido = datos.Keys.FirstOrDefault(clave => Campos.All(campo => campo.Clave != clave));
        if (desconocido is not null)
        {
            throw new DomainException($"El trámite \"{Nombre}\" no tiene el campo \"{desconocido}\".");
        }

        var validados = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var campo in Campos)
        {
            var valor = datos.GetValueOrDefault(campo.Clave)?.Trim();
            if (string.IsNullOrEmpty(valor))
            {
                if (campo.Obligatorio)
                {
                    throw new DomainException($"Completá el campo \"{campo.Etiqueta}\".");
                }

                continue;
            }

            validados[campo.Clave] = ValidarValor(campo, valor, hoy);
        }

        return validados;
    }

    private static string ValidarValor(CampoTramite campo, string valor, DateOnly hoy)
    {
        switch (campo.Tipo)
        {
            case TipoCampo.Fecha:
                if (!DateOnly.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
                {
                    throw new DomainException($"\"{campo.Etiqueta}\" no es una fecha válida.");
                }

                return campo.SinFechasFuturas && fecha > hoy
                    ? throw new DomainException($"\"{campo.Etiqueta}\" no puede ser una fecha futura.")
                    : fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            case TipoCampo.Carrera:
                return Guid.TryParse(valor, out var carreraId) && carreraId != Guid.Empty
                    ? carreraId.ToString()
                    : throw new DomainException($"Elegí una carrera en \"{campo.Etiqueta}\".");

            default:
                var maximo = campo.Tipo == TipoCampo.TextoLargo ? Math.Min(campo.LargoMaximo, LargoMaximoTextoLargo) : campo.LargoMaximo;
                return valor.Length <= maximo
                    ? valor
                    : throw new DomainException($"\"{campo.Etiqueta}\" no puede superar los {maximo} caracteres.");
        }
    }
}
