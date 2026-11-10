using ICanHelp.Application.DTO;
using Microsoft.Extensions.Logging;

namespace ICanHelp.Application.Notifications;

/// <summary>
/// Krok 15: every channel gets the notification (SignalR + push). A failure of one channel is only logged –
/// the others still deliver and the event counts as handled (the notification is already on the list).
/// Push goes always, also when the app is open: in the active tab FCM hands it to the app (<c>onMessage</c>)
/// instead of a system bubble, so there are no double notifications.
/// </summary>
internal sealed class CompositeNotificationSender : INotificationSender
{
    private readonly IEnumerable<INotificationChannel> _channels;
    private readonly ILogger<CompositeNotificationSender> _logger;

    public CompositeNotificationSender(IEnumerable<INotificationChannel> channels,
        ILogger<CompositeNotificationSender> logger)
    {
        _channels = channels;
        _logger = logger;
    }

    public async Task SendAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default)
    {
        foreach (var channel in _channels)
        {
            try
            {
                await channel.SendAsync(userId, notification, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Notification {NotificationId} was not delivered by {Channel}.",
                    notification.Id, channel.GetType().Name);
            }
        }
    }
}
