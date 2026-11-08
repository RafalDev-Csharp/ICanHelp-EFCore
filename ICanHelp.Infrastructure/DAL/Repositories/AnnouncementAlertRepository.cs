using ICanHelp.Core.Entities.Alerts;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
// NetTopologySuite.Geometries ma własny typ Location – tu chodzi o punkt z domeny.
using Location = ICanHelp.Core.ValueObjects.Location;

namespace ICanHelp.Infrastructure.DAL.Repositories;

internal sealed class AnnouncementAlertRepository : IAnnouncementAlertRepository
{
    private readonly ICanHelpDbContext _db;

    public AnnouncementAlertRepository(ICanHelpDbContext db)
    {
        _db = db;
    }

    public Task<AnnouncementAlert?> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
        => _db.AnnouncementAlerts.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<int> CountByUserAsync(UserId userId, CancellationToken cancellationToken = default)
        => _db.AnnouncementAlerts.CountAsync(a => a.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<AlertCandidate>> FindCandidatesAsync(Id categoryId, UserId excludedUserId,
        Location point, CancellationToken cancellationToken = default)
    {
        var origin = Spatial.CreatePoint(point.Latitude, point.Longitude);

        // Zone.STIntersects(@point) = 1 – can use the spatial index IX_AnnouncementAlerts_Zone (the radius is already
        // in the zone, so it does not differ per row in the condition). Distance from the shape: centre or route line.
        var rows = await _db.AnnouncementAlerts
            .Where(a => a.IsEnabled
                        && a.CategoryId == categoryId
                        && a.UserId != excludedUserId
                        && EF.Property<Geometry>(a, Spatial.AlertZone).Intersects(origin))
            .Select(a => new
            {
                Alert = a,
                Distance = EF.Property<Geometry>(a, Spatial.AlertShape).Distance(origin)
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AlertCandidate(r.Alert, r.Distance)).ToList();
    }

    public async Task AddAsync(AnnouncementAlert alert, CancellationToken cancellationToken = default)
        => await _db.AnnouncementAlerts.AddAsync(alert, cancellationToken);

    public void Remove(AnnouncementAlert alert) => _db.AnnouncementAlerts.Remove(alert);
}
