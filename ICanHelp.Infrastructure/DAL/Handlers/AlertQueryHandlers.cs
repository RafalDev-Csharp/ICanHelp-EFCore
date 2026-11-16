using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.Entities.Alerts;
using ICanHelp.Core.Enums;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetMyAlertsHandler(ICanHelpDbContext db) : IQueryHandler<GetMyAlerts, IReadOnlyList<AlertDto>>
{
    public async Task<IReadOnlyList<AlertDto>> HandleAsync(GetMyAlerts query,
        CancellationToken cancellationToken = default)
    {
        var userId = new UserId(query.UserId);

        var alerts = await db.AnnouncementAlerts
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return alerts.Select(a => a.AsDto()).ToList();
    }
}

internal sealed class GetMyAlertHandler(ICanHelpDbContext db) : IQueryHandler<GetMyAlert, AlertDto?>
{
    public async Task<AlertDto?> HandleAsync(GetMyAlert query, CancellationToken cancellationToken = default)
    {
        var userId = new UserId(query.UserId);
        var alertId = new Id(query.AlertId);

        var alert = await db.AnnouncementAlerts
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == alertId && a.UserId == userId, cancellationToken);

        return alert?.AsDto();
    }
}

/// <summary>Otwarte ogłoszenia w strefie alertu teraz (to samo dopasowanie co przy powiadomieniu, bez harmonogramu).</summary>
internal sealed class GetAlertPreviewHandler(ICanHelpDbContext db)
    : IQueryHandler<GetAlertPreview, IReadOnlyList<AnnouncementDto>?>
{
    // Podkategorie filtrujemy w pamięci (lista w JSON) – z bazy bierzemy najwyżej tyle najnowszych kandydatów.
    private const int MaxCandidates = 500;

    public async Task<IReadOnlyList<AnnouncementDto>?> HandleAsync(GetAlertPreview query,
        CancellationToken cancellationToken = default)
    {
        var userId = new UserId(query.UserId);
        var alertId = new Id(query.AlertId);

        var alert = await db.AnnouncementAlerts
            .AsNoTracking()
            .Where(a => a.Id == alertId && a.UserId == userId)
            .Select(a => new
            {
                a.Kind,
                a.CategoryId,
                a.SubcategoryIds,
                Shape = EF.Property<Geometry?>(a, Spatial.AlertShape),
                Zone = EF.Property<Geometry?>(a, Spatial.AlertZone)
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (alert is null)
        {
            return null;
        }

        var everywhere = alert.Kind == AlertKind.Country;
        if (alert.Zone is null && !everywhere)
        {
            return [];
        }

        var zone = alert.Zone;
        var shape = alert.Shape;
        var categoryId = alert.CategoryId;
        var limit = Math.Clamp(query.Limit, 1, Paging.MaxPageSize);

        var open = db.Announcements
            .AsNoTracking()
            .Where(a => a.Status == AnnouncementStatus.Open && a.OwnerId != userId);

        if (categoryId is not null)
        {
            open = open.Where(a => a.CategoryId == categoryId);
        }

        // GeoPoint.STIntersects(@zone) – indeks IX_Announcements_GeoPoint. Cała Polska – bez strefy.
        if (!everywhere)
        {
            open = open.Where(a => EF.Property<Point>(a, Spatial.GeoPoint).Intersects(zone));
        }

        var newest = open
            .OrderByDescending(a => a.CreatedAt)
            .Take(MaxCandidates);

        // Odległość – od środka albo linii trasy; cała Polska nie ma kształtu, więc bez odległości.
        var candidates = await (shape is null ? newest.ToListRows() : newest.ToListRows(distanceFrom: shape))
            .ToListAsync(cancellationToken);

        return candidates
            .Where(x => alert.SubcategoryIds.Count == 0 || alert.SubcategoryIds.Contains(x.Announcement.SubcategoryId))
            .Take(limit)
            .Select(x => x.AsDto())
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
            CategoryId = entity.CategoryId?.Value,
            SubcategoryIds = entity.SubcategoryIds.Select(s => s.Value).ToList(),
            Center = entity.Center?.AsDto(),
            RoutePoints = entity.RoutePoints.Select(p => p.AsDto()).ToList(),
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
