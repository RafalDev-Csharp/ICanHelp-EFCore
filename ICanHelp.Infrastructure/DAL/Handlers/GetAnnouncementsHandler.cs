using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

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

        // Throws InvalidSearchPhraseException (→ 400) for a too long phrase. Title/Description have value converters,
        // so the columns are compared as plain strings via the (string)(object) cast; the default collation of
        // SQL Server is case-insensitive.
        if (query.GetPhrasePattern() is { } pattern)
        {
            announcements = announcements.Where(a =>
                EF.Functions.Like((string)(object)a.Title, pattern, @"\")
                || EF.Functions.Like((string)(object)a.Description, pattern, @"\"));
        }

        var (skip, take) = Extensions.ToSkipTake(query.Page, query.PageSize);

        // Throws InvalidDistanceFilterException (→ 400) for incomplete/out-of-range parameters.
        if (query.GetDistanceFilter() is { } near)
        {
            var origin = Spatial.CreatePoint(near.Latitude, near.Longitude);

            // geography: Distance/IsWithinDistance are in metres. IsWithinDistance → STDistance(...) <= @radius,
            // which can use the spatial index IX_Announcements_GeoPoint.
            var nearest = await announcements
                .Where(a => EF.Property<Point>(a, Spatial.GeoPoint).IsWithinDistance(origin, near.RadiusMeters))
                .Select(a => new
                {
                    Announcement = a,
                    Distance = EF.Property<Point>(a, Spatial.GeoPoint).Distance(origin),
                    ThumbnailId = a.Images.OrderBy(i => i.AddedAt).Select(i => i.Id).FirstOrDefault(),
                    HelperCount = a.Offers.Select(o => o.HelperId).Distinct().Count()
                })
                .OrderBy(x => x.Distance)
                .ThenByDescending(x => x.Announcement.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return nearest
                .Select(x => x.Announcement.AsDto() with
                {
                    DistanceKm = Math.Round(x.Distance / 1000, 2),
                    ThumbnailUrl = ThumbnailUrl(x.Announcement.Id, x.ThumbnailId),
                    HelperCount = x.HelperCount
                })
                .ToList();
        }

        // Thumbnail = the oldest image and HelperCount = distinct helpers with an offer, both as subqueries –
        // the list does not load whole image/offer collections.
        var page = await announcements
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(a => new
            {
                Announcement = a,
                ThumbnailId = a.Images.OrderBy(i => i.AddedAt).Select(i => i.Id).FirstOrDefault(),
                HelperCount = a.Offers.Select(o => o.HelperId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        return page
            .Select(x => x.Announcement.AsDto() with
            {
                ThumbnailUrl = ThumbnailUrl(x.Announcement.Id, x.ThumbnailId),
                HelperCount = x.HelperCount
            })
            .ToList();
    }

    private static string? ThumbnailUrl(Id announcementId, Id? imageId)
        => imageId is null ? null : ImageDto.GetUrl(announcementId, imageId);
}
