using Astrea.Application.Contracts.Results;
using Astrea.Domain.Procedures;

namespace Astrea.Application.Features.Procedures;

public sealed record CampoTramiteDto(string Clave, string Etiqueta, TipoCampo Tipo, bool Obligatorio, int LargoMaximo, string? Ayuda);

public sealed record TramiteTipoDto(Guid Id, string Codigo, string Nombre, string Descripcion, bool Activo, IReadOnlyList<CampoTramiteDto> Campos)
{
    public static TramiteTipoDto Desde(TramiteTipo tipo) =>
        new(
            tipo.Id,
            tipo.Codigo,
            tipo.Nombre,
            tipo.Descripcion,
            tipo.Activo,
            tipo.Campos.Select(campo => new CampoTramiteDto(campo.Clave, campo.Etiqueta, campo.Tipo, campo.Obligatorio, campo.LargoMaximo, campo.Ayuda)).ToList());
}

public sealed record PersonaDto(Guid Id, string Email, string? Nombre, string? Apellido, string? Legajo)
{
    public string NombreParaMostrar => Apellido is not null || Nombre is not null ? $"{Apellido}, {Nombre}".Trim(' ', ',') : Email;
}

/// <summary>Fila de un listado de trámites (bandeja o "mis trámites").</summary>
public sealed record TramiteResumenDto(
    Guid Id,
    string Numero,
    Guid TipoId,
    string TipoNombre,
    EstadoTramite Estado,
    DateTimeOffset FechaSolicitud,
    DateTimeOffset UpdatedAt,
    string SolicitanteEmail,
    string? SolicitanteApellido,
    string? SolicitanteNombre);

/// <summary>Un dato del formulario, con su etiqueta y el valor listo para mostrar (ej. el nombre de la carrera).</summary>
public sealed record DatoTramiteDto(string Clave, string Etiqueta, string Valor);

/// <summary>
/// Un cambio de estado del historial. Para el estudiante, <see cref="Actor"/> es "Vos" o "Bedelía";
/// la administración ve quién fue (auditoría).
/// </summary>
public sealed record EventoTramiteDto(EstadoTramite? EstadoAnterior, EstadoTramite EstadoNuevo, string? Comentario, string Actor, DateTimeOffset Fecha);

public sealed record TramiteDetalleDto(
    Guid Id,
    string Numero,
    Guid TipoId,
    string TipoNombre,
    EstadoTramite Estado,
    DateTimeOffset FechaSolicitud,
    DateTimeOffset UpdatedAt,
    string? ObservacionAdministrativa,
    PersonaDto Solicitante,
    string? AdministradorAsignado,
    IReadOnlyList<DatoTramiteDto> Datos,
    IReadOnlyList<EventoTramiteDto> Historial);

/// <summary>Filtros permitidos para buscar trámites. <see cref="Texto"/> busca por número o por el email/apellido del titular.</summary>
public sealed record FiltroTramites(
    Guid? UsuarioId = null,
    IReadOnlyCollection<EstadoTramite>? Estados = null,
    Guid? TipoId = null,
    string? Texto = null,
    bool MasRecientesPrimero = false);

public sealed record SolicitarTramiteCommand(Guid TipoId, Dictionary<string, string?>? Datos, Guid? ClaveIdempotencia);

public sealed record AprobarTramiteCommand(string? Comentario);

public sealed record RechazarTramiteCommand(string Motivo);

public static class ErroresTramites
{
    public const int LargoMaximoTexto = 100;

    public static readonly Error TramiteNoEncontrado =
        Error.NoEncontrado("tramite_no_encontrado", "El trámite no existe.");

    public static readonly Error TipoNoEncontrado =
        Error.NoEncontrado("tipo_tramite_no_encontrado", "Ese tipo de trámite no existe.");

    public static readonly Error CarreraInvalida =
        Error.Validacion("carrera_invalida", "La carrera elegida no existe o no está activa.");

    public static readonly Error TextoDemasiadoLargo =
        Error.Validacion("texto_demasiado_largo", $"La búsqueda no puede superar los {LargoMaximoTexto} caracteres.");
}

public static class NumeroTramite
{
    /// <summary>Formato para mostrar el número en pantalla y en ventanilla: T-000123.</summary>
    public static string Formatear(int numero) => $"T-{numero:000000}";
}
