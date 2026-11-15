using Astrea.Application.Features.LostAndFound;

namespace Astrea.Api.Mantenimiento;

/// <summary>
/// Al iniciar y después cada <see cref="Intervalo"/>, borra los hallazgos cerrados hace más de 30 días.
/// Si una pasada falla, lo registra y lo vuelve a intentar en la siguiente: nunca tira abajo la API.
/// </summary>
internal sealed class DepuracionPeriodicaHallazgos(IServiceScopeFactory scopes, ILogger<DepuracionPeriodicaHallazgos> logger) : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var borrados = await scope.ServiceProvider.GetRequiredService<DepuracionHallazgos>().DepurarAsync(stoppingToken);
                if (borrados > 0)
                {
                    logger.LogInformation("Se borraron {Cantidad} hallazgos cerrados hace más de 30 días.", borrados);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "No se pudieron depurar los hallazgos vencidos.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
