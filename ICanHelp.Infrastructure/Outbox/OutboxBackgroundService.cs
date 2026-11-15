using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ICanHelp.Infrastructure.Outbox;

/// <summary>Every <see cref="OutboxOptions.Interval"/> processes what is waiting in the outbox (unless turned off).</summary>
internal sealed class OutboxBackgroundService(IOutboxProcessor processor, IOptions<OutboxOptions> options,
    ILogger<OutboxBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Outbox processing is turned off (Outbox:Enabled = false).");
            return;
        }

        using var timer = new PeriodicTimer(options.Value.Interval);

        try
        {
            do
            {
                try
                {
                    await processor.ProcessPendingAsync(stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // E.g. the database is not reachable for a moment – try again in the next round.
                    logger.LogError(exception, "Outbox round failed.");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The application is stopping.
        }
    }
}
