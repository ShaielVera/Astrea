using Astrea.Domain.Common;

namespace Astrea.Domain.Agent;

public enum RolMensaje
{
    Usuario = 1,
    Asistente = 2,
}

/// <summary>
/// Documento en el que se apoyó una respuesta del asistente. Se guarda una copia (título, versión, páginas, extracto)
/// para que el historial siga mostrando de dónde salió la respuesta aunque después se cargue otra versión.
/// </summary>
public sealed record FuenteRespuesta(
    Guid DocumentoId,
    string Titulo,
    int Version,
    string? Seccion,
    string? Fuente,
    int PaginaDesde,
    int PaginaHasta,
    string Extracto);

/// <summary>
/// Historial mínimo de una charla con el asistente. Es privada: solo la ve quien la inició.
/// Se borra sola <see cref="DiasDeConservacion"/> días después del último mensaje.
/// </summary>
public sealed class Conversacion : Entity
{
    public const int LargoMaximoPregunta = 1000;

    public const int LargoMaximoRespuesta = 4000;

    public const int LargoMaximoTitulo = 80;

    /// <summary>Preguntas por conversación: es un historial mínimo, no un archivo.</summary>
    public const int PreguntasMaximas = 50;

    public const int DiasDeConservacion = 30;

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
        bool sinEvidencia,
        DateTimeOffset ahora)
    {
        if (CantidadDePreguntas >= PreguntasMaximas)
        {
            throw new DomainException($"La conversación llegó al máximo de {PreguntasMaximas} preguntas. Empezá una nueva.");
        }

        if (sinEvidencia && fuentes.Count > 0)
        {
            throw new DomainException("Una respuesta sin evidencia no puede citar fuentes.");
        }

        if (!sinEvidencia && fuentes.Count == 0)
        {
            throw new DomainException("Una respuesta sobre información oficial tiene que citar al menos una fuente.");
        }

        var mensajePregunta = new MensajeChat(Guid.CreateVersion7(), Id, RolMensaje.Usuario, Texto.Requerido(pregunta, "texto de la pregunta", LargoMaximoPregunta), [], false, ahora);
        var mensajeRespuesta = new MensajeChat(Guid.CreateVersion7(), Id, RolMensaje.Asistente, Recortar(respuesta), fuentes, sinEvidencia, ahora);
        _mensajes.Add(mensajePregunta);
        _mensajes.Add(mensajeRespuesta);
        FechaUltimoMensaje = ahora;
        return (mensajePregunta, mensajeRespuesta);
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

/// <summary>Una pregunta del usuario o una respuesta del asistente (con sus fuentes).</summary>
public sealed class MensajeChat : Entity
{
    private MensajeChat()
    {
    }

    internal MensajeChat(Guid id, Guid conversacionId, RolMensaje rol, string contenido, IReadOnlyList<FuenteRespuesta> fuentes, bool sinEvidencia, DateTimeOffset fecha)
        : base(id)
    {
        ConversacionId = conversacionId;
        Rol = rol;
        Contenido = contenido;
        Fuentes = [.. fuentes];
        SinEvidencia = sinEvidencia;
        Fecha = fecha;
    }

    public Guid ConversacionId { get; private set; }

    public RolMensaje Rol { get; private set; }

    public string Contenido { get; private set; } = null!;

    public List<FuenteRespuesta> Fuentes { get; private set; } = [];

    /// <summary>El asistente no encontró información confirmada y derivó a Secretaría (sirve para las métricas).</summary>
    public bool SinEvidencia { get; private set; }

    public DateTimeOffset Fecha { get; private set; }
}
