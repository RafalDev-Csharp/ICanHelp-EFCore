using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Notifications;
using ICanHelp.Application.Queries;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

/// <summary>Powiadomienia użytkownika od najnowszych (indeks UserId + ReadAt + CreatedAt).</summary>
internal sealed class GetMyNotificationsHandler : IQueryHandler<GetMyNotifications, IReadOnlyList<NotificationDto>>
{
    private readonly ICanHelpDbContext _db;

    public GetMyNotificationsHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<NotificationDto>> HandleAsync(GetMyNotifications query,
        CancellationToken cancellationToken = default)
    {
        var userId = new UserId(query.UserId);
        var (skip, take) = Extensions.ToSkipTake(query.Page, query.PageSize);

        var notifications = _db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (query.UnreadOnly)
        {
            notifications = notifications.Where(n => n.ReadAt == null);
        }

        var page = await notifications
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return page.Select(n => n.ToDto()).ToList();
    }
}

internal sealed class GetUnreadNotificationCountHandler : IQueryHandler<GetUnreadNotificationCount, UnreadCountDto>
{
    private readonly ICanHelpDbContext _db;

    public GetUnreadNotificationCountHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<UnreadCountDto> HandleAsync(GetUnreadNotificationCount query,
        CancellationToken cancellationToken = default)
    {
        var userId = new UserId(query.UserId);
        var count = await _db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, cancellationToken);
        return new UnreadCountDto(count);
    }
}
