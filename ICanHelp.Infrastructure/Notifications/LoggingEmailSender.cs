using ICanHelp.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace ICanHelp.Infrastructure.Notifications;

/// <summary>Placeholder e-mail channel: only writes to the log (real SMTP later).</summary>
internal sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("E-mail (not sent, logging only) to {To}: {Subject}", to, subject);
        return Task.CompletedTask;
    }
}
