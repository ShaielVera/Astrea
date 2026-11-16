using System.Globalization;
using System.Text;
using Astrea.Domain.Common;
using Astrea.Domain.Exams;

namespace Astrea.Domain.Agent;

public enum ResultadoConsulta
{
    Respondida = 1,
    SinEvidencia = 2,
    Aclaracion = 3,
    Error = 4,
}

/// <summary>Por qué una consulta no terminó en una respuesta con fuentes.</summary>
public enum MotivoConsulta
{
    Ninguno = 0,

    /// <summary>El modelo reconoció que los documentos y datos no alcanzaban.</summary>
    SinInformacion = 1,

    /// <summary>El modelo respondió sin citar fuentes reales: se descartó la respuesta.</summary>
    CitasInvalidas = 2,

    /// <summary>El modelo no devolvió una respuesta utilizable.</summary>
    RespuestaVacia = 3,

    /// <summary>Se agotaron los pasos de herramientas sin llegar a una respuesta.</summary>
    PasosAgotados = 4,

    /// <summary>El proveedor de IA (embeddings o chat) no respondió.</summary>
    ProveedorNoDisponible = 5,
}

/// <summary>
/// Telemetría mínima y anónima de una consulta al asistente, para el panel de métricas de Secretaría.
/// No guarda quién preguntó ni en qué conversación: solo el tema (la pregunta normalizada, sin datos personales),
/// el resultado y cuánto tardó. Se conserva <see cref="DiasDeConservacion"/> días.
/// </summary>
public sealed class ConsultaRegistrada : Entity
{
    public const int LargoMaximoTema = 200;

    public const int DiasDeConservacion = 365;

    private ConsultaRegistrada(Guid id)
        : base(id)
    {
    }

    private ConsultaRegistrada()
    {
    }

    public DateTimeOffset Fecha { get; private set; }

    /// <summary>Día del instituto (hora argentina) en que se hizo, para agrupar por día.</summary>
    public DateOnly Dia { get; private set; }

    public string Tema { get; private set; } = string.Empty;

    public ResultadoConsulta Resultado { get; private set; }

    public MotivoConsulta Motivo { get; private set; }

    public bool ConPropuesta { get; private set; }

    public int FuentesDocumentos { get; private set; }

    public int FuentesDatos { get; private set; }

    public List<string> Herramientas { get; private set; } = [];

    public int DuracionMs { get; private set; }

    public static ConsultaRegistrada Registrar(
        Guid id,
        string pregunta,
        ResultadoConsulta resultado,
        MotivoConsulta motivo,
        DateTimeOffset fecha,
        TimeSpan duracion,
        IEnumerable<string>? herramientas = null,
        int fuentesDocumentos = 0,
        int fuentesDatos = 0,
        bool conPropuesta = false)
    {
        if (!Enum.IsDefined(resultado) || !Enum.IsDefined(motivo))
        {
            throw new DomainException("El resultado de la consulta no es válido.");
        }

        var sinRespuesta = resultado is ResultadoConsulta.SinEvidencia or ResultadoConsulta.Error;
        if (sinRespuesta == (motivo == MotivoConsulta.Ninguno))
        {
            throw new DomainException("Una consulta respondida no tiene motivo de falla, y una sin respuesta debe tenerlo.");
        }

        if (fuentesDocumentos < 0 || fuentesDatos < 0)
        {
            throw new DomainException("La cantidad de fuentes no puede ser negativa.");
        }

        var tema = Normalizar(pregunta);
        return new ConsultaRegistrada(id)
        {
            Fecha = fecha,
            Dia = ZonaHorariaInstitucional.Hoy(fecha),
            Tema = tema.Length == 0 ? "(vacía)" : tema,
            Resultado = resultado,
            Motivo = motivo,
            ConPropuesta = conPropuesta,
            FuentesDocumentos = fuentesDocumentos,
            FuentesDatos = fuentesDatos,
            Herramientas = herramientas?.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList() ?? [],
            DuracionMs = (int)Math.Clamp(duracion.TotalMilliseconds, 0, int.MaxValue),
        };
    }

    /// <summary>
    /// La pregunta como tema agrupable: sin datos personales, en minúsculas, sin tildes ni signos y con espacios simples.
    /// "¿Cuándo cierra la inscripción?" y "cuando cierra la inscripcion" cuentan como el mismo tema.
    /// </summary>
    public static string Normalizar(string pregunta)
    {
        var oculta = DatosSensibles.Ocultar(pregunta ?? string.Empty);
        var sinTildes = oculta.Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(sinTildes.Length);
        var espacioPendiente = false;
        foreach (var caracter in sinTildes)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            // Los marcadores de datos ocultos ([DNI], [email]) se conservan para que se entienda qué se preguntó.
            if (char.IsLetterOrDigit(caracter) || caracter is '[' or ']')
            {
                if (espacioPendiente && resultado.Length > 0)
                {
                    resultado.Append(' ');
                }

                espacioPendiente = false;
                resultado.Append(char.ToLowerInvariant(caracter));
            }
            else
            {
                espacioPendiente = true;
            }
        }

        var tema = resultado.ToString();
        return tema.Length <= LargoMaximoTema ? tema : tema[..LargoMaximoTema].TrimEnd();
    }
}
