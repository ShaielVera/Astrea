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

/// <summary>Datos de una persona. El DNI solo se completa en la vista de administración y la del propio titular.</summary>
public sealed record PersonaDto(Guid Id, string Email, string? Nombre, string? Apellido, string? Legajo, string? Dni = null)
{
    public string NombreParaMostrar => Apellido is not null || Nombre is not null ? $"{Apellido}, {Nombre}".Trim(' ', ',') : Email;

    public bool TieneDatosOficiales => Nombre is not null && Apellido is not null && Dni is not null;
}

/// <summary>PDF emitido para un trámite.</summary>
public sealed record DocumentoDto(
    Guid Id,
    string TipoDocumento,
    string CodigoValidacion,
    string UrlValidacion,
    DateTimeOffset FechaEmision,
    EstadoDocumento Estado,
    int VersionPlantilla,
    string HashSha256,
    string? MotivoAnulacion);

/// <summary>
/// Lo que muestra la validación pública de un QR: autenticidad, tipo, fecha y estado (SDD §9).
/// Del titular, solo los últimos 3 dígitos del DNI.
/// </summary>
public sealed record ValidacionDocumentoDto(
    string CodigoValidacion,
    string TipoDocumento,
    string NumeroTramite,
    DateTimeOffset FechaEmision,
    EstadoDocumento Estado,
    string? DniParcial,
    string HashSha256);

/// <summary>Un archivo para descargar.</summary>
public sealed record ArchivoDto(string NombreArchivo, string ContentType, byte[] Contenido);

public sealed record DatosOficialesCommand(string Nombre, string Apellido, string Dni, string? Legajo);

public sealed record AnularDocumentoCommand(string Motivo);

public sealed record NuevaVersionPlantillaCommand(string Titulo, string Cuerpo);

/// <summary>Plantilla de un tipo de trámite para el panel de administración. <see cref="Vigente"/> es null si el tipo no emite PDF.</summary>
public sealed record PlantillaDeTipoDto(Guid TipoId, string TipoNombre, PlantillaDto? Vigente, IReadOnlyList<string> MarcadoresDisponibles);

public sealed record PlantillaDto(Guid Id, int Version, string Titulo, string Cuerpo, DateTimeOffset CreadaAt);

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
    IReadOnlyList<EventoTramiteDto> Historial,
    bool EmiteDocumento,
    DocumentoDto? Documento);

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

    public static readonly Error DatosOficialesIncompletos =
        Error.Validacion(
            "datos_oficiales_incompletos",
            "Antes de aprobar, cargá los datos oficiales del estudiante (nombre, apellido y DNI): van impresos en el documento.");

    public static readonly Error DocumentoNoEncontrado =
        Error.NoEncontrado("documento_no_encontrado", "No hay un documento emitido para este trámite.");

    public static readonly Error DocumentoAnulado =
        Error.Conflicto("documento_anulado", "El documento de este trámite fue anulado y ya no se puede descargar.");

    public static readonly Error CodigoInvalido =
        Error.NoEncontrado("codigo_invalido", "No existe un documento con ese código.");

    public static readonly Error UsuarioNoEncontrado =
        Error.NoEncontrado("usuario_no_encontrado", "El usuario no existe.");
}

public static class NumeroTramite
{
    /// <summary>Formato para mostrar el número en pantalla y en ventanilla: T-000123.</summary>
    public static string Formatear(int numero) => $"T-{numero:000000}";
}

public static class FormatoDni
{
    /// <summary>"30123456" → "30.123.456".</summary>
    public static string Formatear(string dni) =>
        dni.Length > 3 ? $"{Formatear(dni[..^3])}.{dni[^3..]}" : dni;

    /// <summary>Para la validación pública: solo los últimos 3 dígitos ("**.***.456").</summary>
    public static string? Parcial(string? ultimosDigitos) => ultimosDigitos is null ? null : $"**.***.{ultimosDigitos}";
}
