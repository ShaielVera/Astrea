using Astrea.Domain.Common;

namespace Astrea.Domain.Agent;

public enum RolMensaje
{
    Usuario = 1,
    Asistente = 2,
}

/// <summary>De dónde sale un dato de la respuesta: un documento oficial o los datos vigentes del sistema.</summary>
public enum TipoFuente
{
    Documento = 0,
    DatosDeAstrea = 1,
}

/// <summary>
/// En qué se apoyó una respuesta del asistente. Para un documento se guarda una copia (título, versión, páginas, extracto)
/// para que el historial siga mostrando de dónde salió aunque después se cargue otra versión. Para datos de Astrea
/// (mesas, correlatividades…) se guarda un resumen de lo consultado y la sección donde verlo actualizado.
/// </summary>
public sealed record FuenteRespuesta(
    Guid? DocumentoId,
    string Titulo,
    int? Version,
    string? Seccion,
    string? Fuente,
    int? PaginaDesde,
    int? PaginaHasta,
    string Extracto,
    TipoFuente Tipo = TipoFuente.Documento,
    string? Enlace = null);

public enum EstadoPropuesta
{
    Pendiente = 1,
    Confirmada = 2,
    Cancelada = 3,
}

/// <summary>Un dato de la propuesta tal como se le muestra al estudiante (ej. "Carrera: Profesorado de Matemática").</summary>
public sealed record DatoPropuesta(string Etiqueta, string Valor);

/// <summary>
/// Solicitud de trámite que preparó el asistente. NO es un trámite: recién se crea cuando el estudiante la confirma
/// (una sola vez, aunque confirme dos veces). Vence a las <see cref="Conversacion.HorasDeVigenciaPropuesta"/> horas.
/// </summary>
public sealed record PropuestaTramite(
    Guid TipoId,
    string TipoNombre,
    Dictionary<string, string?> Datos,
    IReadOnlyList<DatoPropuesta> Resumen,
    EstadoPropuesta Estado = EstadoPropuesta.Pendiente,
    Guid? TramiteId = null,
    string? NumeroTramite = null);

/// <summary>Cómo terminó una respuesta del asistente.</summary>
public enum TipoRespuesta
{
    /// <summary>Respuesta con fuentes (o con una propuesta de trámite para confirmar).</summary>
    Respuesta = 1,

    /// <summary>No encontró información confirmada y derivó a Secretaría.</summary>
    SinEvidencia = 2,

    /// <summary>Le pide al estudiante un dato que falta (ej. de qué carrera). No afirma nada.</summary>
    Aclaracion = 3,
}

/// <summary>
/// Historial mínimo de una charla con el asistente. Es privada: solo la ve quien la inició.
/// Se borra sola <see cref="DiasDeConservacion"/> días después del último mensaje.
/// </summary>
public sealed class Conversacion : Entity
{
    public const int LargoMaximoPregunta = 1000;

    public const int LargoMaximoRespuesta = 4000;

    public const int LargoMaximoTitulo = 80;

    /// <summary>Una aclaración es una pregunta corta, no una respuesta encubierta.</summary>
    public const int LargoMaximoAclaracion = 300;

    /// <summary>Preguntas por conversación: es un historial mínimo, no un archivo.</summary>
    public const int PreguntasMaximas = 50;

    public const int DiasDeConservacion = 30;

    public const int HorasDeVigenciaPropuesta = 24;

    private readonly List<MensajeChat> _mensajes = [];

    private Conversacion()
    {
    }

    private Conversacion(Guid id, Guid usuarioId, string primeraPregunta, DateTimeOffset ahora)
        : base(id)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new DomainException("La conversación debe tener un titular.");
        }

        UsuarioId = usuarioId;
        Titulo = TituloDesde(primeraPregunta);
        FechaCreacion = ahora;
        FechaUltimoMensaje = ahora;
    }

    public Guid UsuarioId { get; private set; }

    /// <summary>El comienzo de la primera pregunta, para reconocerla en el listado.</summary>
    public string Titulo { get; private set; } = null!;

    public DateTimeOffset FechaCreacion { get; private set; }

    public DateTimeOffset FechaUltimoMensaje { get; private set; }

    public IReadOnlyList<MensajeChat> Mensajes => _mensajes;

    public int CantidadDePreguntas => _mensajes.Count(mensaje => mensaje.Rol == RolMensaje.Usuario);

    public static Conversacion Iniciar(Guid id, Guid usuarioId, string primeraPregunta, DateTimeOffset ahora) =>
        new(id, usuarioId, primeraPregunta, ahora);

    public bool PerteneceA(Guid usuarioId) => UsuarioId == usuarioId;

    /// <summary>Los últimos mensajes, del más viejo al más nuevo: el contexto que se le pasa al modelo.</summary>
    public IReadOnlyList<MensajeChat> UltimosMensajes(int cantidad) =>
        _mensajes.OrderBy(mensaje => mensaje.Fecha).ThenBy(mensaje => mensaje.Rol).TakeLast(cantidad).ToList();

    /// <summary>La última pregunta anterior, para entender repreguntas como "¿y para la regularidad?".</summary>
    public string? UltimaPregunta =>
        _mensajes.Where(mensaje => mensaje.Rol == RolMensaje.Usuario).OrderBy(mensaje => mensaje.Fecha).LastOrDefault()?.Contenido;

    public bool DebeDepurarse(DateTimeOffset ahora) => FechaUltimoMensaje <= ahora.AddDays(-DiasDeConservacion);

    /// <summary>Registra una pregunta y su respuesta juntas: nunca queda una pregunta sin contestar en el historial.</summary>
    public (MensajeChat Pregunta, MensajeChat Respuesta) Registrar(
        string pregunta,
        string respuesta,
        IReadOnlyList<FuenteRespuesta> fuentes,
        TipoRespuesta tipo,
        DateTimeOffset ahora,
        PropuestaTramite? propuesta = null)
    {
        if (CantidadDePreguntas >= PreguntasMaximas)
        {
            throw new DomainException($"La conversación llegó al máximo de {PreguntasMaximas} preguntas. Empezá una nueva.");
        }

        ValidarRespuesta(respuesta, fuentes, tipo, propuesta);

        var mensajePregunta = new MensajeChat(Guid.CreateVersion7(), Id, RolMensaje.Usuario, Texto.Requerido(pregunta, "texto de la pregunta", LargoMaximoPregunta), [], TipoRespuesta.Respuesta, null, ahora);
        var mensajeRespuesta = new MensajeChat(Guid.CreateVersion7(), Id, RolMensaje.Asistente, Recortar(respuesta), fuentes, tipo, propuesta, ahora);
        _mensajes.Add(mensajePregunta);
        _mensajes.Add(mensajeRespuesta);
        FechaUltimoMensaje = ahora;
        return (mensajePregunta, mensajeRespuesta);
    }

    /// <summary>La propuesta pendiente de un mensaje, si todavía se puede confirmar o cancelar.</summary>
    public PropuestaTramite PropuestaPendiente(Guid mensajeId, DateTimeOffset ahora)
    {
        var mensaje = _mensajes.SingleOrDefault(m => m.Id == mensajeId && m.Propuesta is not null)
            ?? throw new DomainException("Ese mensaje no tiene una solicitud para confirmar.");
        var propuesta = mensaje.Propuesta!;
        if (propuesta.Estado != EstadoPropuesta.Pendiente)
        {
            throw new DomainException(propuesta.Estado == EstadoPropuesta.Confirmada
                ? $"Esa solicitud ya se envió (trámite {propuesta.NumeroTramite})."
                : "Esa solicitud se canceló. Pedile al asistente una nueva.");
        }

        if (ahora > mensaje.Fecha.AddHours(HorasDeVigenciaPropuesta))
        {
            throw new DomainException("Esa solicitud venció. Pedile al asistente que la prepare de nuevo.");
        }

        return propuesta;
    }

    public MensajeChat ConfirmarPropuesta(Guid mensajeId, Guid tramiteId, string numeroTramite, DateTimeOffset ahora)
    {
        var propuesta = PropuestaPendiente(mensajeId, ahora);
        var mensaje = _mensajes.Single(m => m.Id == mensajeId);
        mensaje.ActualizarPropuesta(propuesta with { Estado = EstadoPropuesta.Confirmada, TramiteId = tramiteId, NumeroTramite = numeroTramite });
        FechaUltimoMensaje = ahora;
        return mensaje;
    }

    public MensajeChat CancelarPropuesta(Guid mensajeId, DateTimeOffset ahora)
    {
        var propuesta = PropuestaPendiente(mensajeId, ahora);
        var mensaje = _mensajes.Single(m => m.Id == mensajeId);
        mensaje.ActualizarPropuesta(propuesta with { Estado = EstadoPropuesta.Cancelada });
        FechaUltimoMensaje = ahora;
        return mensaje;
    }

    private static void ValidarRespuesta(string respuesta, IReadOnlyList<FuenteRespuesta> fuentes, TipoRespuesta tipo, PropuestaTramite? propuesta)
    {
        if (!Enum.IsDefined(tipo))
        {
            throw new DomainException("El tipo de respuesta no es válido.");
        }

        if (tipo != TipoRespuesta.Respuesta && (fuentes.Count > 0 || propuesta is not null))
        {
            throw new DomainException("Una respuesta sin evidencia o una aclaración no pueden citar fuentes ni proponer trámites.");
        }

        if (tipo == TipoRespuesta.Respuesta && fuentes.Count == 0 && propuesta is null)
        {
            throw new DomainException("Una respuesta sobre información oficial tiene que citar al menos una fuente.");
        }

        if (tipo == TipoRespuesta.Aclaracion && respuesta.Trim().Length > LargoMaximoAclaracion)
        {
            throw new DomainException($"Una aclaración no puede superar los {LargoMaximoAclaracion} caracteres.");
        }

        if (propuesta is not null && propuesta.Estado != EstadoPropuesta.Pendiente)
        {
            throw new DomainException("Una propuesta nueva tiene que quedar pendiente de confirmación.");
        }
    }

    private static string TituloDesde(string pregunta)
    {
        var limpia = string.Join(' ', Texto.Requerido(pregunta, "texto de la pregunta", LargoMaximoPregunta).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return limpia.Length <= LargoMaximoTitulo ? limpia : $"{limpia[..(LargoMaximoTitulo - 1)].TrimEnd()}…";
    }

    private static string Recortar(string respuesta)
    {
        var limpia = Texto.Requerido(respuesta, "texto de la respuesta", int.MaxValue);
        return limpia.Length <= LargoMaximoRespuesta ? limpia : $"{limpia[..(LargoMaximoRespuesta - 1)].TrimEnd()}…";
    }
}

/// <summary>Una pregunta del usuario o una respuesta del asistente (con sus fuentes y, si corresponde, una propuesta de trámite).</summary>
public sealed class MensajeChat : Entity
{
    private MensajeChat()
    {
    }

    internal MensajeChat(
        Guid id,
        Guid conversacionId,
        RolMensaje rol,
        string contenido,
        IReadOnlyList<FuenteRespuesta> fuentes,
        TipoRespuesta tipo,
        PropuestaTramite? propuesta,
        DateTimeOffset fecha)
        : base(id)
    {
        ConversacionId = conversacionId;
        Rol = rol;
        Contenido = contenido;
        Fuentes = [.. fuentes];
        SinEvidencia = tipo == TipoRespuesta.SinEvidencia;
        EsAclaracion = tipo == TipoRespuesta.Aclaracion;
        Propuesta = propuesta;
        Fecha = fecha;
    }

    public Guid ConversacionId { get; private set; }

    public RolMensaje Rol { get; private set; }

    public string Contenido { get; private set; } = null!;

    public List<FuenteRespuesta> Fuentes { get; private set; } = [];

    /// <summary>El asistente no encontró información confirmada y derivó a Secretaría (sirve para las métricas).</summary>
    public bool SinEvidencia { get; private set; }

    /// <summary>El asistente pidió un dato que faltaba en lugar de responder.</summary>
    public bool EsAclaracion { get; private set; }

    public PropuestaTramite? Propuesta { get; private set; }

    public DateTimeOffset Fecha { get; private set; }

    internal void ActualizarPropuesta(PropuestaTramite propuesta) => Propuesta = propuesta;
}
