using ICanHelp.Core.Entities.Notifications;
using ICanHelp.Core.Enums;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Repositories;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Id id, CancellationToken cancellationToken = default);

    /// <summary>Unread notifications of the user (for "mark all as read").</summary>
    Task<IReadOnlyList<Notification>> GetUnreadAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>Was a notification for this user already created from this event? (idempotency)</summary>
    Task<bool> ExistsAsync(UserId userId, Guid sourceEventId, CancellationToken cancellationToken = default);

    /// <summary>How many notifications of the type the user got since the time (daily limit of alerts).</summary>
    Task<int> CountSinceAsync(UserId userId, NotificationType type, DateTime sinceUtc,
        CancellationToken cancellationToken = default);

    /// <summary>The newest notification of the type since the time (today's digest), tracked for changes.</summary>
    Task<Notification?> GetLatestSinceAsync(UserId userId, NotificationType type, DateTime sinceUtc,
        CancellationToken cancellationToken = default);

    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);
}
