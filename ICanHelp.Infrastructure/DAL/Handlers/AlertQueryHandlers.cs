using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.Entities.Alerts;
using ICanHelp.Core.Enums;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetMyAlertsHandler : IQueryHandler<GetMyAlerts, IReadOnlyList<AlertDto>>
{
    private readonly ICanHelpDbContext _db;

    public GetMyAlertsHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AlertDto>> HandleAsync(GetMyAlerts query,
        CancellationToken cancellationToken = default)
    {
        var userId = new UserId(query.UserId);

        var alerts = await _db.AnnouncementAlerts
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return alerts.Select(a => a.AsDto()).ToList();
    }
}

internal sealed class GetMyAlertHandler : IQueryHandler<GetMyAlert, AlertDto?>
{
    private readonly ICanHelpDbContext _db;

    public GetMyAlertHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<AlertDto?> HandleAsync(GetMyAlert query, CancellationToken cancellationToken = default)
    {
        var userId = new UserId(query.UserId);
        var alertId = new Id(query.AlertId);

        var alert = await _db.AnnouncementAlerts
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == alertId && a.UserId == userId, cancellationToken);

        return alert?.AsDto();
    }
}

/// <summary>Otwarte ogłoszenia w strefie alertu teraz (to samo dopasowanie co przy powiadomieniu, bez harmonogramu).</summary>
internal sealed class GetAlertPreviewHandler : IQueryHandler<GetAlertPreview, IReadOnlyList<AnnouncementDto>?>
{
    // Podkategorie filtrujemy w pamięci (lista w JSON) – z bazy bierzemy najwyżej tyle najnowszych kandydatów.
    private const int MaxCandidates = 500;

    private readonly ICanHelpDbContext _db;

    public GetAlertPreviewHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AnnouncementDto>?> HandleAsync(GetAlertPreview query,
        CancellationToken cancellationToken = default)
    {
        var userId = new UserId(query.UserId);
        var alertId = new Id(query.AlertId);

        var alert = await _db.AnnouncementAlerts
            .AsNoTracking()
            .Where(a => a.Id == alertId && a.UserId == userId)
            .Select(a => new
            {
                a.CategoryId,
                a.SubcategoryIds,
                Shape = EF.Property<Geometry>(a, Spatial.AlertShape),
                Zone = EF.Property<Geometry>(a, Spatial.AlertZone)
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (alert is null)
        {
            return null;
        }

        if (alert.Zone is null)
        {
            return [];
        }

        var zone = alert.Zone;
        var shape = alert.Shape;
        var categoryId = alert.CategoryId;
        var limit = Math.Clamp(query.Limit, 1, 100);

        // GeoPoint.STIntersects(@zone) – indeks IX_Announcements_GeoPoint.
        var candidates = await _db.Announcements
            .AsNoTracking()
            .Where(a => a.Status == AnnouncementStatus.Open
                        && a.CategoryId == categoryId
                        && a.OwnerId != userId
                        && EF.Property<Point>(a, Spatial.GeoPoint).Intersects(zone))
            .OrderByDescending(a => a.CreatedAt)
            .Take(MaxCandidates)
            .Select(a => new
            {
                Announcement = a,
                Distance = EF.Property<Point>(a, Spatial.GeoPoint).Distance(shape),
                ThumbnailId = a.Images.OrderBy(i => i.AddedAt).Select(i => i.Id).FirstOrDefault(),
                HelperCount = a.Offers.Select(o => o.HelperId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        return candidates
            .Where(x => alert.SubcategoryIds.Count == 0 || alert.SubcategoryIds.Contains(x.Announcement.SubcategoryId))
            .Take(limit)
            .Select(x => x.Announcement.AsDto() with
            {
                DistanceKm = Math.Round(x.Distance / 1000, 2),
                ThumbnailUrl = x.ThumbnailId is null ? null : ImageDto.GetUrl(x.Announcement.Id, x.ThumbnailId),
                HelperCount = x.HelperCount
            })
            .ToList();
    }
}

internal static class AlertDtoMapping
{
    public static AlertDto AsDto(this AnnouncementAlert entity)
        => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Kind = entity.Kind,
            CategoryId = entity.CategoryId,
            SubcategoryIds = entity.SubcategoryIds.Select(s => s.Value).ToList(),
            Center = entity.Center is { } center
                ? new LocationDto { Latitude = center.Latitude, Longitude = center.Longitude }
                : null,
            RoutePoints = entity.RoutePoints
                .Select(p => new LocationDto { Latitude = p.Latitude, Longitude = p.Longitude })
                .ToList(),
            RadiusKm = entity.RadiusKm,
            Schedule = new AlertScheduleDto
            {
                Days = entity.ActiveDays,
                From = entity.ActiveFrom,
                To = entity.ActiveTo
            },
            IsEnabled = entity.IsEnabled,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
}
