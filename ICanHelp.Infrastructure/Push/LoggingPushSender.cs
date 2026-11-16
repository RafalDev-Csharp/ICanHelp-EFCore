using ICanHelp.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace ICanHelp.Infrastructure.Push;

/// <summary>Push without Firebase (no key, Development, Testing): only writes to the log, no token is invalid.</summary>
internal sealed class LoggingPushSender(ILogger<LoggingPushSender> logger) : IPushSender
{
    public Task<PushSendResult> SendAsync(IReadOnlyCollection<string> tokens, PushMessage message,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Push (not sent, logging only) to {Count} device(s): {Title}", tokens.Count,
            message.Title);
        return Task.FromResult(PushSendResult.None);
    }
}
