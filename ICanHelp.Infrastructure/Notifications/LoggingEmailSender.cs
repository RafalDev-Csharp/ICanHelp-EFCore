using ICanHelp.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace ICanHelp.Infrastructure.Notifications;

/// <summary>Placeholder e-mail channel: only writes to the log (real SMTP later).</summary>
internal sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("E-mail (not sent, logging only) to {To}: {Subject}", to, subject);
        return Task.CompletedTask;
    }
}
