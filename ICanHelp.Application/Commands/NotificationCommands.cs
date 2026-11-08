using ICanHelp.Application.Abstractions;

namespace ICanHelp.Application.Commands;

/// <summary>The recipient read one notification. Somebody else's notification → 404 (we do not reveal it exists).</summary>
public sealed record MarkNotificationAsRead(Guid NotificationId, Guid UserId) : ICommand;

/// <summary>The user read all his notifications (the unread counter becomes 0).</summary>
public sealed record MarkAllNotificationsAsRead(Guid UserId) : ICommand;
