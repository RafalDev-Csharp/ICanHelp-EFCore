using ICanHelp.Application.DTO;
using ICanHelp.Application.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace ICanHelp.Api.Notifications;

/// <summary>Kanał SignalR. Błąd dostarczenia tylko logujemy – powiadomienie jest już zapisane na liście.</summary>
internal sealed class SignalRNotificationSender(IHubContext<NotificationsHub> hub,
    ILogger<SignalRNotificationSender> logger) : INotificationChannel
{
    public async Task SendAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default)
    {
        try
        {
            await hub.Clients.Group(NotificationsHub.GroupOf(userId))
                .SendAsync(NotificationsHub.NotificationMethod, notification, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Notification {NotificationId} was not delivered by SignalR.", notification.Id);
        }
    }
}
