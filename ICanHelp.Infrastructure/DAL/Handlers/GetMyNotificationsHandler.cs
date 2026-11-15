using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Notifications;
using ICanHelp.Application.Queries;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

/// <summary>Powiadomienia użytkownika od najnowszych (indeks UserId + ReadAt + CreatedAt).</summary>
internal sealed class GetMyNotificationsHandler(ICanHelpDbContext db)
    : IQueryHandler<GetMyNotifications, IReadOnlyList<NotificationDto>>
{
    public async Task<IReadOnlyList<NotificationDto>> HandleAsync(GetMyNotifications query,
        CancellationToken cancellationToken = default)
    {
        var userId = new UserId(query.UserId);

        var notifications = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (query.UnreadOnly)
        {
            notifications = notifications.Where(n => n.ReadAt == null);
        }

        var page = await notifications
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .TakePage(query.Page, query.PageSize)
            .ToListAsync(cancellationToken);

        return page.Select(n => n.ToDto()).ToList();
    }
}

internal sealed class GetUnreadNotificationCountHandler(ICanHelpDbContext db)
    : IQueryHandler<GetUnreadNotificationCount, UnreadCountDto>
{
    public async Task<UnreadCountDto> HandleAsync(GetUnreadNotificationCount query,
        CancellationToken cancellationToken = default)
    {
        var userId = new UserId(query.UserId);
        var count = await db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, cancellationToken);
        return new UnreadCountDto(count);
    }
}
