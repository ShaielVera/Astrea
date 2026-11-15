using Astrea.Domain.Common;

namespace Astrea.Domain.Procedures;

/// <summary>
/// Estados de un trámite. Pendiente → En proceso → Listo, o Rechazado (con motivo) desde Pendiente o En proceso.
/// Rechazado y Listo son finales.
/// </summary>
public enum EstadoTramite
{
    Pendiente = 1,
    EnProceso = 2,
    Rechazado = 3,
    Listo = 4,
}

/// <summary>
/// Solicitud de un estudiante. Cada cambio de estado queda en <see cref="Eventos"/> con quién lo hizo y cuándo,
/// así el estudiante ve el historial y la administración tiene auditoría.
/// </summary>
public sealed class Tramite : Entity
{
    private readonly List<TramiteEvento> _eventos = [];

    private Tramite()
    {
    }

    private Tramite(Guid id, Guid usuarioId, Guid tramiteTipoId, IReadOnlyDictionary<string, string> datos, Guid? claveIdempotencia, DateTimeOffset ahora)
        : base(id)
    {
        UsuarioId = usuarioId;
        TramiteTipoId = tramiteTipoId;
        Datos = datos;
        ClaveIdempotencia = claveIdempotencia;
        Estado = EstadoTramite.Pendiente;
        FechaSolicitud = ahora;
        UpdatedAt = ahora;
    }

    /// <summary>Número correlativo para hablar del trámite en ventanilla. Lo asigna la base al guardarlo.</summary>
    public int Numero { get; private set; }

    /// <summary>Titular: el estudiante que lo pidió. Es el único estudiante que puede verlo.</summary>
    public Guid UsuarioId { get; private set; }

    public Guid TramiteTipoId { get; private set; }

    /// <summary>Datos del formulario, ya validados contra los campos del tipo.</summary>
    public IReadOnlyDictionary<string, string> Datos { get; private set; } = new Dictionary<string, string>();

    /// <summary>Generada por el cliente: si la misma solicitud llega dos veces (doble clic, reintento), se crea una sola.</summary>
    public Guid? ClaveIdempotencia { get; private set; }

    public EstadoTramite Estado { get; private set; }

    /// <summary>Motivo del rechazo, o indicación al estudiante cuando queda listo (ej. "Retirala en Bedelía").</summary>
    public string? ObservacionAdministrativa { get; private set; }

    public Guid? AdministradorAsignadoId { get; private set; }

    public DateTimeOffset FechaSolicitud { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<TramiteEvento> Eventos => _eventos;

    public bool EstaFinalizado => Estado is EstadoTramite.Rechazado or EstadoTramite.Listo;

    public static Tramite Solicitar(
        Guid id,
        Guid usuarioId,
        TramiteTipo tipo,
        IReadOnlyDictionary<string, string?>? datos,
        Guid? claveIdempotencia,
        DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        if (usuarioId == Guid.Empty)
        {
            throw new DomainException("El trámite debe tener un titular.");
        }

        if (!tipo.Activo)
        {
            throw new DomainException($"El trámite \"{tipo.Nombre}\" no se puede pedir por ahora.");
        }

        var tramite = new Tramite(id, usuarioId, tipo.Id, tipo.ValidarDatos(datos), claveIdempotencia, ahora);
        tramite.Registrar(null, EstadoTramite.Pendiente, null, usuarioId, ahora);
        return tramite;
    }

    public bool EsTitular(Guid usuarioId) => UsuarioId == usuarioId;

    /// <summary>Un administrador lo toma: pasa a "En proceso" y queda asignado a él.</summary>
    public void Tomar(Guid administradorId, DateTimeOffset ahora)
    {
        if (Estado != EstadoTramite.Pendiente)
        {
            throw new DomainException(EstaFinalizado
                ? "El trámite ya está finalizado."
                : "El trámite ya está en proceso.");
        }

        AdministradorAsignadoId = administradorId;
        Registrar(Estado, EstadoTramite.EnProceso, null, administradorId, ahora);
    }

    /// <summary>
    /// Lo aprueba: queda "Listo" para retirar o descargar. El comentario lo ve el estudiante.
    /// Es idempotente: aprobar un trámite que ya está listo (un reintento) no hace nada.
    /// </summary>
    public void Aprobar(Guid administradorId, string? comentario, DateTimeOffset ahora)
    {
        if (Estado == EstadoTramite.Listo)
        {
            return;
        }

        ValidarAbierto();
        ObservacionAdministrativa = Texto.Opcional(comentario, "comentario", 500);
        AdministradorAsignadoId ??= administradorId;
        Registrar(Estado, EstadoTramite.Listo, ObservacionAdministrativa, administradorId, ahora);
    }

    /// <summary>Lo rechaza. El motivo es obligatorio: el estudiante tiene que saber qué pasó.</summary>
    public void Rechazar(Guid administradorId, string motivo, DateTimeOffset ahora)
    {
        ValidarAbierto();
        ObservacionAdministrativa = Texto.Requerido(motivo, "motivo del rechazo", 500);
        AdministradorAsignadoId ??= administradorId;
        Registrar(Estado, EstadoTramite.Rechazado, ObservacionAdministrativa, administradorId, ahora);
    }

    private void ValidarAbierto()
    {
        if (EstaFinalizado)
        {
            throw new DomainException("El trámite ya está finalizado.");
        }
    }

    private void Registrar(EstadoTramite? anterior, EstadoTramite nuevo, string? comentario, Guid actorId, DateTimeOffset ahora)
    {
        if (actorId == Guid.Empty)
        {
            throw new DomainException("Todo cambio de estado debe registrar quién lo hizo.");
        }

        Estado = nuevo;
        UpdatedAt = ahora;
        _eventos.Add(new TramiteEvento(Guid.CreateVersion7(), Id, anterior, nuevo, comentario, actorId, ahora));
    }
}

/// <summary>Un cambio de estado de un trámite: quién, cuándo, de qué estado a cuál y con qué comentario.</summary>
public sealed class TramiteEvento : Entity
{
    private TramiteEvento()
    {
    }

    internal TramiteEvento(Guid id, Guid tramiteId, EstadoTramite? estadoAnterior, EstadoTramite estadoNuevo, string? comentario, Guid actorUsuarioId, DateTimeOffset fecha)
        : base(id)
    {
        TramiteId = tramiteId;
        EstadoAnterior = estadoAnterior;
        EstadoNuevo = estadoNuevo;
        Comentario = comentario;
        ActorUsuarioId = actorUsuarioId;
        Fecha = fecha;
    }

    public Guid TramiteId { get; private set; }

    /// <summary>Nulo en el evento de creación.</summary>
    public EstadoTramite? EstadoAnterior { get; private set; }

    public EstadoTramite EstadoNuevo { get; private set; }

    public string? Comentario { get; private set; }

    public Guid ActorUsuarioId { get; private set; }

    public DateTimeOffset Fecha { get; private set; }
}
