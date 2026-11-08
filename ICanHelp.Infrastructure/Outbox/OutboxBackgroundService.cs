using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ICanHelp.Infrastructure.Outbox;

/// <summary>Every <see cref="OutboxOptions.Interval"/> processes what is waiting in the outbox (unless turned off).</summary>
internal sealed class OutboxBackgroundService : BackgroundService
{
    private readonly IOutboxProcessor _processor;
    private readonly IOptions<OutboxOptions> _options;
    private readonly ILogger<OutboxBackgroundService> _logger;

    public OutboxBackgroundService(IOutboxProcessor processor, IOptions<OutboxOptions> options,
        ILogger<OutboxBackgroundService> logger)
    {
        _processor = processor;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled)
        {
            _logger.LogInformation("Outbox processing is turned off (Outbox:Enabled = false).");
            return;
        }

        using var timer = new PeriodicTimer(_options.Value.Interval);

        try
        {
            do
            {
                try
                {
                    await _processor.ProcessPendingAsync(stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // E.g. the database is not reachable for a moment – try again in the next round.
                    _logger.LogError(exception, "Outbox round failed.");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The application is stopping.
        }
    }
}
