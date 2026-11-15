using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.Entities;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetAnnouncementsHandler(ICanHelpDbContext db)
    : IQueryHandler<GetAnnouncements, IReadOnlyList<AnnouncementDto>>
{
    public async Task<IReadOnlyList<AnnouncementDto>> HandleAsync(GetAnnouncements query,
        CancellationToken cancellationToken = default)
    {
        // Read side: no tracking, no aggregate rules - just data for the screen.
        var announcements = ApplyFilters(db.Announcements.AsNoTracking(), query);

        // Throws InvalidDistanceFilterException (→ 400) for incomplete/out-of-range parameters.
        var page = query.GetDistanceFilter() is { } near
            ? NearestFirst(announcements, near, query)
            : announcements.OrderByDescending(a => a.CreatedAt).TakePage(query.Page, query.PageSize).ToListRows();

        var rows = await page.ToListAsync(cancellationToken);
        return rows.Select(row => row.AsDto()).ToList();
    }

    private static IQueryable<Announcement> ApplyFilters(IQueryable<Announcement> announcements, GetAnnouncements query)
    {
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

        // Throws InvalidSearchPhraseException (→ 400) for a too long phrase. Title/Description have value converters,
        // so the columns are compared as plain strings via the (string)(object) cast; the default collation of
        // SQL Server is case-insensitive.
        if (query.GetPhrasePattern() is { } pattern)
        {
            announcements = announcements.Where(a =>
                EF.Functions.Like((string)(object)a.Title, pattern, @"\")
                || EF.Functions.Like((string)(object)a.Description, pattern, @"\"));
        }

        return announcements;
    }

    // geography: Distance/IsWithinDistance are in metres. IsWithinDistance → STDistance(...) <= @radius,
    // which can use the spatial index IX_Announcements_GeoPoint.
    private static IQueryable<AnnouncementListRow> NearestFirst(IQueryable<Announcement> announcements,
        DistanceFilter near, GetAnnouncements query)
    {
        var origin = Spatial.CreatePoint(near.Latitude, near.Longitude);
        var radiusMeters = near.RadiusMeters;

        return announcements
            .Where(a => EF.Property<Point>(a, Spatial.GeoPoint).IsWithinDistance(origin, radiusMeters))
            .OrderBy(a => EF.Property<Point>(a, Spatial.GeoPoint).Distance(origin))
            .ThenByDescending(a => a.CreatedAt)
            .TakePage(query.Page, query.PageSize)
            .ToListRows(origin);
    }
}
