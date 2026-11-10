using ICanHelp.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace ICanHelp.Infrastructure.Push;

/// <summary>Push without Firebase (no key, Development, Testing): only writes to the log, no token is invalid.</summary>
internal sealed class LoggingPushSender : IPushSender
{
    private readonly ILogger<LoggingPushSender> _logger;

    public LoggingPushSender(ILogger<LoggingPushSender> logger)
    {
        _logger = logger;
    }

    public Task<PushSendResult> SendAsync(IReadOnlyCollection<string> tokens, PushMessage message,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Push (not sent, logging only) to {Count} device(s): {Title}", tokens.Count,
            message.Title);
        return Task.FromResult(PushSendResult.None);
    }
}
