namespace ICanHelp.Application.Notifications;

/// <summary>
/// E-mail channel. For now the implementation only logs (real SMTP later). Planned use: a digest of unread
/// notifications for users who are not in the app – not one e-mail per message.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}
