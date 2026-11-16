using Astrea.Domain.Academic;

namespace Astrea.Application.Features.Academic;

public sealed record CarreraDto(Guid Id, string Codigo, string Nombre, int DuracionAnios, bool Activa);

public sealed record PlanEstudioDto(
    Guid Id,
    Guid CarreraId,
    string CodigoVersion,
    string Nombre,
    DateOnly? VigenciaDesde,
    DateOnly? VigenciaHasta,
    bool Activo,
    int CantidadMaterias);

public sealed record MateriaDePlanDto(
    Guid PlanMateriaId,
    Guid MateriaId,
    string Codigo,
    string Nombre,
    int Anio,
    Cuatrimestre Cuatrimestre,
    int Orden,
    int? CargaHorariaSemanal,
    bool Obligatoria,
    bool Activa);

/// <summary>
/// Un requisito de correlatividad. Si <see cref="MateriaId"/> es nulo, es una condición en texto
/// (<see cref="Descripcion"/>) que verifica Secretaría. Los requisitos con el mismo <see cref="GrupoAlternativa"/>
/// son alternativas entre sí; los que no tienen grupo son todos obligatorios.
/// </summary>
public sealed record RequisitoDto(
    Guid Id,
    CondicionRequisito Condicion,
    int? GrupoAlternativa,
    Guid? MateriaId,
    string? Codigo,
    string? Nombre,
    int? Anio,
    string? Descripcion);

public sealed record CorrelatividadesDeMateriaDto(
    Guid PlanId,
    Guid PlanMateriaId,
    Guid MateriaId,
    string Codigo,
    string Nombre,
    int Anio,
    IReadOnlyList<RequisitoDto> ParaCursar,
    IReadOnlyList<RequisitoDto> ParaRendir);

public sealed record VerificacionCorrelatividadesDto(
    TipoCorrelatividad Tipo,
    ResultadoVerificacion Resultado,
    IReadOnlyList<RequisitoDto> Faltantes,
    IReadOnlyList<RequisitoDto> AVerificar);

public static class ErroresAcademicos
{
    public static readonly Contracts.Results.Error CorrelatividadNoEncontrada =
        Contracts.Results.Error.NoEncontrado("academico.correlatividad_no_encontrada", "La correlatividad no existe en este plan.");

    public static readonly Contracts.Results.Error CarreraNoEncontrada =
        Contracts.Results.Error.NoEncontrado("academico.carrera_no_encontrada", "La carrera no existe.");

    public static readonly Contracts.Results.Error PlanNoEncontrado =
        Contracts.Results.Error.NoEncontrado("academico.plan_no_encontrado", "El plan de estudios no existe.");

    public static readonly Contracts.Results.Error MateriaNoEncontrada =
        Contracts.Results.Error.NoEncontrado("academico.materia_no_encontrada", "La materia no forma parte de este plan.");

    public static Contracts.Results.Error CodigoCarreraDuplicado(string codigo) =>
        Contracts.Results.Error.Conflicto("academico.codigo_duplicado", $"Ya existe una carrera con el código {codigo}.");
}

/// <summary>Una materia dentro de un plan vigente, encontrada por nombre.</summary>
public sealed record MateriaEncontradaDto(Guid PlanId, Guid MateriaId, string Codigo, string Nombre, int Anio, Guid CarreraId, string Carrera, string Plan);
