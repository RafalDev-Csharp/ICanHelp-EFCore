using ICanHelp.Core.Entities.Notifications;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Repositories;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Id id, CancellationToken cancellationToken = default);

    /// <summary>Unread notifications of the user (for "mark all as read").</summary>
    Task<IReadOnlyList<Notification>> GetUnreadAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>Was a notification for this user already created from this event? (idempotency)</summary>
    Task<bool> ExistsAsync(UserId userId, Guid sourceEventId, CancellationToken cancellationToken = default);

    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);
}
