using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetAnnouncementsHandler : IQueryHandler<GetAnnouncements, IReadOnlyList<AnnouncementDto>>
{
    private readonly ICanHelpDbContext _db;

    public GetAnnouncementsHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AnnouncementDto>> HandleAsync(GetAnnouncements query,
        CancellationToken cancellationToken = default)
    {
        // Read side: no tracking, no aggregate rules - just data for the screen.
        var announcements = _db.Announcements.AsNoTracking();

        if (query.OwnerId is { } ownerId)
        {
            var id = new UserId(ownerId);
            announcements = announcements.Where(a => a.OwnerId == id);
        }

        if (query.CategoryId is { } categoryId)
        {
            var id = new Id(categoryId);
            announcements = announcements.Where(a => a.CategoryId == id);
        }

        if (query.SubcategoryId is { } subcategoryId)
        {
            var id = new Id(subcategoryId);
            announcements = announcements.Where(a => a.SubcategoryId == id);
        }

        if (query.Status is { } status)
        {
            announcements = announcements.Where(a => a.Status == status);
        }

        var (skip, take) = Extensions.ToSkipTake(query.Page, query.PageSize);

        var page = await announcements
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return page.Select(a => a.AsDto()).ToList();
    }
}
