using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Notifications;

/// <summary>
/// Delivers a notification that is already saved – what the event handlers call, after the commit.
/// The implementation (<see cref="CompositeNotificationSender"/>) passes it to every <see cref="INotificationChannel"/>.
/// A failed delivery must not fail the handling – the notification stays on the list.
/// </summary>
public interface INotificationSender
{
    Task SendAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// One way of delivering (Krok 13: SignalR to the open app – registered by Api; Krok 15: push to the user's devices).
/// </summary>
public interface INotificationChannel
{
    Task SendAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default);
}
