using ICanHelp.Application.DTO;
using ICanHelp.Application.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace ICanHelp.Api.Notifications;

/// <summary>Kanał SignalR. Błąd dostarczenia tylko logujemy – powiadomienie jest już zapisane na liście.</summary>
internal sealed class SignalRNotificationSender : INotificationSender
{
    private readonly IHubContext<NotificationsHub> _hub;
    private readonly ILogger<SignalRNotificationSender> _logger;

    public SignalRNotificationSender(IHubContext<NotificationsHub> hub, ILogger<SignalRNotificationSender> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task SendAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hub.Clients.Group(NotificationsHub.GroupOf(userId))
                .SendAsync(NotificationsHub.NotificationMethod, notification, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Notification {NotificationId} was not delivered by SignalR.", notification.Id);
        }
    }
}
