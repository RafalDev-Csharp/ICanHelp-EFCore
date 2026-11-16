using ICanHelp.Application.Abstractions;

namespace ICanHelp.Application.Commands;

/// <summary>The recipient read one notification. Somebody else's notification → 404 (we do not reveal it exists).</summary>
public sealed record MarkNotificationAsRead(Guid NotificationId, Guid UserId) : ICommand;

/// <summary>
/// The user read all his notifications (the unread counter becomes 0). With <paramref name="ConversationId"/> – only
/// those of one conversation (the app opened the thread, so its messages / offers are seen).
/// </summary>
public sealed record MarkAllNotificationsAsRead(Guid UserId, Guid? ConversationId = null) : ICommand;
