using Astrea.Application.Features.Agent;
using Astrea.Application.Features.LostAndFound;

namespace Astrea.Api.Mantenimiento;

/// <summary>
/// Al iniciar y después cada <see cref="Intervalo"/>, borra lo que ya no corresponde conservar: los hallazgos cerrados
/// hace más de 30 días, las conversaciones con el asistente sin actividad hace más de 30 días y la telemetría
/// anónima del asistente con más de un año.
/// Si una pasada falla, lo registra y lo vuelve a intentar en la siguiente: nunca tira abajo la API.
/// </summary>
internal sealed class DepuracionPeriodica(IServiceScopeFactory scopes, ILogger<DepuracionPeriodica> logger) : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo);
        do
        {
            await DepurarAsync<DepuracionHallazgos>((servicio, ct) => servicio.DepurarAsync(ct), "hallazgos cerrados", stoppingToken);
            await DepurarAsync<DepuracionConversaciones>((servicio, ct) => servicio.DepurarAsync(ct), "conversaciones con el asistente", stoppingToken);
            await DepurarAsync<DepuracionTelemetria>((servicio, ct) => servicio.DepurarAsync(ct), "registros de métricas del asistente", stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DepurarAsync<TServicio>(Func<TServicio, CancellationToken, Task<int>> depurar, string que, CancellationToken stoppingToken)
        where TServicio : notnull
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var borrados = await depurar(scope.ServiceProvider.GetRequiredService<TServicio>(), stoppingToken);
            if (borrados > 0)
            {
                logger.LogInformation("Se borraron {Cantidad} {Que} vencidos.", borrados, que);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "No se pudieron depurar los {Que} vencidos.", que);
        }
    }
}
