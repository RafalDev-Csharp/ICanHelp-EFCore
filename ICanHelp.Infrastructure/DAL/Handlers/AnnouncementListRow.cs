using ICanHelp.Core.Entities;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace ICanHelp.Infrastructure.DAL.Handlers;

/// <summary>
/// One announcement on a list (search, alert preview): the entity plus what the card needs but the list does not
/// load – the thumbnail (the oldest image) and the number of helpers with an offer, both computed by subqueries,
/// so whole image / offer collections are never read. With a search point also the distance from it.
/// </summary>
internal sealed class AnnouncementListRow
{
    public required Announcement Announcement { get; init; }
    public Id? ThumbnailId { get; init; }
    public int HelperCount { get; init; }

    /// <summary>Metres from the search point or the alert shape (null when the list is not searched by location).</summary>
    public double? DistanceMeters { get; init; }
}

internal static class AnnouncementListQueries
{
    /// <summary>The last step of a list query – after filtering, ordering and paging.</summary>
    public static IQueryable<AnnouncementListRow> ToListRows(this IQueryable<Announcement> announcements)
        => announcements.Select(a => new AnnouncementListRow
        {
            Announcement = a,
            ThumbnailId = a.Images.OrderBy(i => i.AddedAt).Select(i => i.Id).FirstOrDefault(),
            HelperCount = a.Offers.Select(o => o.HelperId).Distinct().Count()
        });

    /// <summary>As above, with the distance (geography → metres) from <paramref name="distanceFrom"/>.</summary>
    public static IQueryable<AnnouncementListRow> ToListRows(this IQueryable<Announcement> announcements,
        Geometry distanceFrom)
        => announcements.Select(a => new AnnouncementListRow
        {
            Announcement = a,
            ThumbnailId = a.Images.OrderBy(i => i.AddedAt).Select(i => i.Id).FirstOrDefault(),
            HelperCount = a.Offers.Select(o => o.HelperId).Distinct().Count(),
            DistanceMeters = EF.Property<Point>(a, Spatial.GeoPoint).Distance(distanceFrom)
        });
}
