using Astrea.Application.Contracts.Results;
using Astrea.Domain.LostAndFound;

namespace Astrea.Application.Features.LostAndFound;

/// <summary>
/// Lo que ve cualquiera en la búsqueda. A propósito NO incluye la descripción privada, quién lo publicó
/// ni datos de la entrega: con eso alguien podría reclamar un objeto que no es suyo (SDD RF-08).
/// </summary>
public sealed record HallazgoPublicoDto(
    Guid Id,
    string TituloPublico,
    CategoriaObjeto Categoria,
    string SectorEncontrado,
    FranjaHoraria FranjaHoraria,
    PuntoResguardo PuntoResguardo,
    DateOnly FechaHallazgo,
    DateTimeOffset FechaPublicacion,
    bool TieneFoto)
{
    public static HallazgoPublicoDto Desde(ObjetoPerdido objeto) =>
        new(
            objeto.Id,
            objeto.TituloPublico,
            objeto.Categoria,
            objeto.SectorEncontrado,
            objeto.FranjaHoraria,
            objeto.PuntoResguardo,
            objeto.FechaHallazgo,
            objeto.FechaPublicacion,
            objeto.FotoStoragePath is not null);
}

/// <summary>Una publicación propia: quien la hizo ve también su descripción privada y en qué quedó.</summary>
public sealed record MiHallazgoDto(
    Guid Id,
    string TituloPublico,
    string DescripcionPrivada,
    CategoriaObjeto Categoria,
    string SectorEncontrado,
    FranjaHoraria FranjaHoraria,
    PuntoResguardo PuntoResguardo,
    DateOnly FechaHallazgo,
    DateTimeOffset FechaPublicacion,
    bool TieneFoto,
    EstadoObjeto Estado,
    DateTimeOffset? FechaCierre)
{
    public static MiHallazgoDto Desde(ObjetoPerdido objeto) =>
        new(
            objeto.Id,
            objeto.TituloPublico,
            objeto.DescripcionPrivada,
            objeto.Categoria,
            objeto.SectorEncontrado,
            objeto.FranjaHoraria,
            objeto.PuntoResguardo,
            objeto.FechaHallazgo,
            objeto.FechaPublicacion,
            objeto.FotoStoragePath is not null,
            objeto.Estado,
            objeto.FechaCierre);
}

public sealed record PersonaHallazgoDto(Guid Id, string Email, string? Nombre, string? Apellido);

public sealed record EntregaDto(string NombreRetirante, string DniRetirante, bool IdentidadVerificada, string VerificadoPor, string? ObservacionInterna, DateTimeOffset FechaEntrega);

/// <summary>Vista completa para la Secretaría: incluye lo privado, el publicador y la auditoría de la entrega o la baja.</summary>
public sealed record HallazgoAdminDto(
    Guid Id,
    string TituloPublico,
    string DescripcionPrivada,
    CategoriaObjeto Categoria,
    string SectorEncontrado,
    FranjaHoraria FranjaHoraria,
    PuntoResguardo PuntoResguardo,
    DateOnly FechaHallazgo,
    DateTimeOffset FechaPublicacion,
    bool TieneFoto,
    EstadoObjeto Estado,
    DateTimeOffset? FechaCierre,
    PersonaHallazgoDto PublicadoPor,
    EntregaDto? Entrega,
    string? MotivoBaja,
    string? DadoDeBajaPor);

/// <summary>Filtros permitidos. <see cref="Texto"/> busca en el título y el lugar, sin distinguir tildes. <see cref="SoloConFoto"/> deja afuera los que todavía no tienen foto.</summary>
public sealed record FiltroHallazgos(
    IReadOnlyCollection<EstadoObjeto> Estados,
    string? Texto = null,
    CategoriaObjeto? Categoria = null,
    PuntoResguardo? Punto = null,
    Guid? PublicadoPor = null,
    bool SoloConFoto = false);

public sealed record PublicarHallazgoCommand(
    string TituloPublico,
    string DescripcionPrivada,
    CategoriaObjeto Categoria,
    string SectorEncontrado,
    FranjaHoraria FranjaHoraria,
    PuntoResguardo PuntoResguardo,
    DateOnly FechaHallazgo);

public sealed record EntregarHallazgoCommand(string NombreRetirante, string DniRetirante, bool IdentidadVerificada, string? ObservacionInterna);

public sealed record DarDeBajaHallazgoCommand(string Motivo);

public static class ErroresHallazgos
{
    public const int LargoMaximoTexto = 100;

    public static readonly Error NoEncontrado =
        Error.NoEncontrado("hallazgo_no_encontrado", "El hallazgo no existe o ya no está publicado.");

    public static readonly Error SinFoto =
        Error.NoEncontrado("hallazgo_sin_foto", "El hallazgo no tiene foto.");

    public static readonly Error SinPermiso =
        Error.Prohibido("hallazgo_sin_permiso", "Solo quien publicó el hallazgo o la Secretaría pueden cambiar la foto.");

    public static readonly Error TextoDemasiadoLargo =
        Error.Validacion("texto_demasiado_largo", $"La búsqueda no puede superar los {LargoMaximoTexto} caracteres.");
}
