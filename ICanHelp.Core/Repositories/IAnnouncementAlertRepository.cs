using ICanHelp.Core.Entities.Alerts;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Repositories;

/// <summary>An alert whose zone contains the point, with the distance from its shape (centre or route line) in metres.</summary>
public sealed record AlertCandidate(AnnouncementAlert Alert, double DistanceMeters);

public interface IAnnouncementAlertRepository
{
    Task<AnnouncementAlert?> GetByIdAsync(Id id, CancellationToken cancellationToken = default);

    Task<int> CountByUserAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enabled alerts of the category whose zone (shape + radius) contains <paramref name="point"/>, without the alerts
    /// of <paramref name="excludedUserId"/> (the owner of the announcement). Geometry is computed by the database
    /// (spatial index); subcategories and the schedule are checked by the domain (<see cref="AnnouncementAlert.Matches"/>).
    /// </summary>
    Task<IReadOnlyList<AlertCandidate>> FindCandidatesAsync(Id categoryId, UserId excludedUserId, Location point,
        CancellationToken cancellationToken = default);

    Task AddAsync(AnnouncementAlert alert, CancellationToken cancellationToken = default);

    void Remove(AnnouncementAlert alert);
}
