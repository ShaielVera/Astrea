using Astrea.Application.Contracts.Results;
using Astrea.Domain.Agent;

namespace Astrea.Application.Features.Agent;

/// <param name="ConversacionId">Null para empezar una conversación nueva.</param>
public sealed record PreguntaCommand(Guid? ConversacionId, string Mensaje);

/// <summary>Una solicitud de trámite preparada por el asistente, para confirmar o cancelar hasta <see cref="VenceEl"/>.</summary>
public sealed record PropuestaDto(EstadoPropuesta Estado, string TipoNombre, IReadOnlyList<DatoPropuesta> Resumen, Guid? TramiteId, string? NumeroTramite, DateTimeOffset VenceEl);

public sealed record MensajeDto(
    Guid Id,
    RolMensaje Rol,
    string Contenido,
    IReadOnlyList<FuenteRespuesta> Fuentes,
    bool SinEvidencia,
    bool EsAclaracion,
    PropuestaDto? Propuesta,
    DateTimeOffset Fecha)
{
    public static MensajeDto Desde(MensajeChat mensaje) =>
        new(
            mensaje.Id,
            mensaje.Rol,
            mensaje.Contenido,
            mensaje.Fuentes,
            mensaje.SinEvidencia,
            mensaje.EsAclaracion,
            mensaje.Propuesta is { } propuesta
                ? new PropuestaDto(propuesta.Estado, propuesta.TipoNombre, propuesta.Resumen, propuesta.TramiteId, propuesta.NumeroTramite, mensaje.Fecha.AddHours(Conversacion.HorasDeVigenciaPropuesta))
                : null,
            mensaje.Fecha);
}

/// <summary>Lo que devuelve una pregunta: la pregunta tal como quedó guardada (con datos personales ocultos) y la respuesta.</summary>
public sealed record IntercambioDto(Guid ConversacionId, string Titulo, MensajeDto Pregunta, MensajeDto Respuesta);

public sealed record ConversacionResumenDto(Guid Id, string Titulo, DateTimeOffset FechaCreacion, DateTimeOffset FechaUltimoMensaje);

public sealed record ConversacionDto(Guid Id, string Titulo, DateTimeOffset FechaCreacion, DateTimeOffset FechaUltimoMensaje, IReadOnlyList<MensajeDto> Mensajes)
{
    public static ConversacionDto Desde(Conversacion conversacion) => new(
        conversacion.Id,
        conversacion.Titulo,
        conversacion.FechaCreacion,
        conversacion.FechaUltimoMensaje,
        conversacion.Mensajes.OrderBy(mensaje => mensaje.Fecha).ThenBy(mensaje => mensaje.Rol).Select(MensajeDto.Desde).ToList());
}

public static class ErroresAsistente
{
    public static readonly Error PreguntaVacia =
        Error.Validacion("pregunta_vacia", "Escribí tu pregunta.");

    public static readonly Error PreguntaDemasiadoLarga =
        Error.Validacion("pregunta_demasiado_larga", $"La pregunta no puede superar los {Conversacion.LargoMaximoPregunta} caracteres.");

    public static readonly Error ConversacionNoEncontrada =
        Error.NoEncontrado("conversacion_no_encontrada", "La conversación no existe.");

    public static readonly Error ConversacionLlena =
        Error.Conflicto("conversacion_llena", $"Esta conversación llegó al máximo de {Conversacion.PreguntasMaximas} preguntas. Empezá una nueva.");

    public static readonly Error FuenteNoDisponible =
        Error.NoEncontrado("fuente_no_disponible", "Ese documento ya no está disponible.");

    public static readonly Error MensajeNoEncontrado =
        Error.NoEncontrado("mensaje_no_encontrado", "El mensaje no existe.");

    public static readonly Error NoDisponible =
        Error.NoDisponible("asistente_no_disponible", "El asistente no está disponible en este momento. Probá de nuevo en unos minutos o consultá en Secretaría.");
}
