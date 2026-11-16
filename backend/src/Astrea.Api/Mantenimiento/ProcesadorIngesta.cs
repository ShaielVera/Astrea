using Astrea.Application.Features.Knowledge;

namespace Astrea.Api.Mantenimiento;

/// <summary>Señal entre la carga de un documento y el procesador: despierta al procesador sin esperar el próximo ciclo.</summary>
internal sealed class AvisoIngesta : IAvisoIngesta
{
    private readonly SemaphoreSlim _senal = new(0);

    public void Avisar()
    {
        // Con una señal pendiente alcanza: el procesador vacía toda la cola cuando despierta.
        if (_senal.CurrentCount == 0)
        {
            _senal.Release();
        }
    }

    public Task<bool> EsperarAsync(TimeSpan maximo, CancellationToken cancellationToken) => _senal.WaitAsync(maximo, cancellationToken);
}

/// <summary>
/// Procesa en segundo plano los documentos pendientes de indexar (un proceso por servidor).
/// Al arrancar devuelve a la cola lo que quedó a medias; después espera avisos de carga o revisa cada <see cref="Revision"/>.
/// </summary>
internal sealed class ProcesadorIngesta(IServiceScopeFactory scopes, AvisoIngesta aviso, ILogger<ProcesadorIngesta> logger) : BackgroundService
{
    public static readonly TimeSpan Revision = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EjecutarAsync(async ingesta =>
        {
            var recuperados = await ingesta.RecuperarInterrumpidosAsync(stoppingToken);
            if (recuperados > 0)
            {
                logger.LogWarning("{Cantidad} documentos habían quedado a medio procesar y volvieron a la cola.", recuperados);
            }
        }, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await EjecutarAsync(async ingesta =>
            {
                foreach (var resultado in await ingesta.ProcesarPendientesAsync(stoppingToken))
                {
                    if (resultado.Indexado)
                    {
                        logger.LogInformation("Documento \"{Titulo}\" indexado: {Fragmentos} fragmentos.", resultado.Titulo, resultado.Fragmentos);
                    }
                    else
                    {
                        logger.LogWarning(resultado.Excepcion, "No se pudo indexar \"{Titulo}\": {Error}", resultado.Titulo, resultado.Error);
                    }
                }
            }, stoppingToken);

            try
            {
                await aviso.EsperarAsync(Revision, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task EjecutarAsync(Func<IngestaDocumentos, Task> accion, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await accion(scope.ServiceProvider.GetRequiredService<IngestaDocumentos>());
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            // Ej. la base no está disponible: se reintenta en la próxima vuelta sin tirar abajo la API.
            logger.LogError(ex, "Falló el procesamiento de documentos pendientes.");
        }
        catch (Exception) when (stoppingToken.IsCancellationRequested)
        {
            // Se está apagando el servidor: lo que quedó a medias se recupera al volver a arrancar.
        }
    }
}
