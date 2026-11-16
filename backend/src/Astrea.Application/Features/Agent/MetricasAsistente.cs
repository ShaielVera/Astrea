using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Results;
using Astrea.Domain.Agent;
using Astrea.Domain.Exams;

namespace Astrea.Application.Features.Agent;

public sealed record ResumenConsultasDto(
    int Total,
    int Respondidas,
    int SinEvidencia,
    int Aclaraciones,
    int Errores,
    int PropuestasDeTramite,
    int DuracionPromedioMs)
{
    /// <summary>Porcentaje de consultas respondidas con fuentes (las aclaraciones no cuentan). Null si no hubo consultas.</summary>
    public int? PorcentajeRespondidas => Total == 0 ? null : (int)Math.Round(100.0 * Respondidas / Total);
}

public sealed record ConsultasDelDiaDto(DateOnly Dia, int Total, int SinRespuesta);

public sealed record TemaDto(string Tema, int Cantidad, DateTimeOffset Ultima);

public sealed record CantidadDto(string Nombre, int Cantidad);

public sealed record MetricasAsistenteDto(
    DateOnly Desde,
    DateOnly Hasta,
    ResumenConsultasDto Resumen,
    IReadOnlyList<ConsultasDelDiaDto> PorDia,
    IReadOnlyList<TemaDto> Frecuentes,
    IReadOnlyList<TemaDto> SinRespuesta,
    IReadOnlyList<CantidadDto> Herramientas,
    IReadOnlyList<CantidadDto> Motivos);

/// <summary>
/// Panel de métricas del asistente para Secretaría: cuánto se usa, qué se pregunta, qué temas quedan sin respuesta
/// (para cargar el documento que falta) y cuántas veces falló la IA. Los datos son anónimos: nunca dicen quién preguntó.
/// </summary>
public sealed class MetricasAsistente(ITelemetriaAsistente telemetria, IClock clock)
{
    public const int DiasPorDefecto = 30;

    public const int DiasMaximos = 366;

    /// <summary>Un tema aparece entre los frecuentes solo si se preguntó al menos esta cantidad de veces: una pregunta única podría identificar a quien la hizo.</summary>
    public const int MinimoFrecuencia = 2;

    public const int MaximoTemas = 10;

    public async Task<Result<MetricasAsistenteDto>> ObtenerAsync(DateOnly? desde, DateOnly? hasta, CancellationToken cancellationToken)
    {
        var fin = hasta ?? ZonaHorariaInstitucional.Hoy(clock.UtcNow);
        var inicio = desde ?? fin.AddDays(-(DiasPorDefecto - 1));
        if (inicio > fin)
        {
            return Error.Validacion("periodo_invalido", "La fecha 'desde' no puede ser posterior a 'hasta'.");
        }

        if (fin.DayNumber - inicio.DayNumber + 1 > DiasMaximos)
        {
            return Error.Validacion("periodo_invalido", $"El período no puede superar los {DiasMaximos} días.");
        }

        var metricas = await telemetria.ObtenerMetricasAsync(inicio, fin, MinimoFrecuencia, MaximoTemas, cancellationToken);

        // Todos los días del período, aunque no haya habido consultas, para que el gráfico no saltee fechas.
        var porDia = metricas.PorDia.ToDictionary(dia => dia.Dia);
        var dias = Enumerable.Range(0, fin.DayNumber - inicio.DayNumber + 1)
            .Select(i => inicio.AddDays(i))
            .Select(dia => porDia.GetValueOrDefault(dia) ?? new ConsultasDelDiaDto(dia, 0, 0))
            .ToList();

        return metricas with { Desde = inicio, Hasta = fin, PorDia = dias };
    }
}

/// <summary>Borra la telemetría del asistente con más de <see cref="ConsultaRegistrada.DiasDeConservacion"/> días.</summary>
public sealed class DepuracionTelemetria(ITelemetriaAsistente telemetria, IClock clock)
{
    public Task<int> DepurarAsync(CancellationToken cancellationToken) =>
        telemetria.EliminarAnterioresAsync(clock.UtcNow.AddDays(-ConsultaRegistrada.DiasDeConservacion), cancellationToken);
}
