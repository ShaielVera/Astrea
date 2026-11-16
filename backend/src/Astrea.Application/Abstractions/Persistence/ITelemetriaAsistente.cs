using Astrea.Application.Features.Agent;
using Astrea.Domain.Agent;

namespace Astrea.Application.Abstractions.Persistence;

/// <summary>Registro anónimo de las consultas al asistente y sus agregaciones para el panel de métricas.</summary>
public interface ITelemetriaAsistente
{
    void Agregar(ConsultaRegistrada consulta);

    /// <summary>
    /// Totales, consultas por día (solo los días con consultas), temas frecuentes (los que se repiten al menos
    /// <paramref name="minimoFrecuencia"/> veces), temas sin respuesta, herramientas usadas y motivos de falla,
    /// entre <paramref name="desde"/> y <paramref name="hasta"/> (días del instituto, ambos incluidos).
    /// </summary>
    Task<MetricasAsistenteDto> ObtenerMetricasAsync(DateOnly desde, DateOnly hasta, int minimoFrecuencia, int maximoTemas, CancellationToken cancellationToken);

    /// <summary>Borra los registros anteriores a <paramref name="limite"/>. Devuelve cuántos borró.</summary>
    Task<int> EliminarAnterioresAsync(DateTimeOffset limite, CancellationToken cancellationToken);
}
