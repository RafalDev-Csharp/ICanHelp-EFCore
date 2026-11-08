using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Notifications;

/// <summary>
/// A delivery channel for notifications that are already saved (Krok 13: SignalR to the open app; later push).
/// Called after the commit. A failed delivery must not fail the handling – the notification stays on the list.
/// </summary>
public interface INotificationSender
{
    Task SendAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default);
}
