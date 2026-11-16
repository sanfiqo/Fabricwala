using TextileConnect.Application.Orders;

namespace TextileConnect.Api;

/// <summary>Runs order housekeeping (acceptance expiry, unpaid expiry, refund retries) once a minute.</summary>
public sealed class OrderMaintenanceWorker(IServiceScopeFactory scopes, ILogger<OrderMaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Order maintenance worker: started (first run now, then every minute)");
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var cancelled = await scope.ServiceProvider.GetRequiredService<IOrderMaintenance>().ExpireAsync(stoppingToken);
                if (cancelled > 0) logger.LogInformation("Order maintenance cancelled {Count} seller orders", cancelled);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError("Order maintenance failed {ExceptionType}", exception.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
