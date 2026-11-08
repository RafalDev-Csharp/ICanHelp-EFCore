using ICanHelp.Core.Entities.Notifications;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Repositories;

internal sealed class NotificationRepository : INotificationRepository
{
    private readonly ICanHelpDbContext _db;

    public NotificationRepository(ICanHelpDbContext db)
    {
        _db = db;
    }

    public Task<Notification?> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
        => _db.Notifications.SingleOrDefaultAsync(n => n.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Notification>> GetUnreadAsync(UserId userId,
        CancellationToken cancellationToken = default)
        => await _db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(UserId userId, Guid sourceEventId, CancellationToken cancellationToken = default)
        => _db.Notifications.AnyAsync(n => n.UserId == userId && n.SourceEventId == sourceEventId, cancellationToken);

    public Task<int> CountSinceAsync(UserId userId, NotificationType type, DateTime sinceUtc,
        CancellationToken cancellationToken = default)
        => _db.Notifications.CountAsync(
            n => n.UserId == userId && n.Type == type && n.CreatedAt >= sinceUtc, cancellationToken);

    public Task<Notification?> GetLatestSinceAsync(UserId userId, NotificationType type, DateTime sinceUtc,
        CancellationToken cancellationToken = default)
        => _db.Notifications
            .Where(n => n.UserId == userId && n.Type == type && n.CreatedAt >= sinceUtc)
            .OrderByDescending(n => n.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
        => await _db.Notifications.AddAsync(notification, cancellationToken);
}
