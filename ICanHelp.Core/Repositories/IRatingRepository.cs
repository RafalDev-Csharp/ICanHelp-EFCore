using ICanHelp.Core.Entities;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Repositories;

public interface IRatingRepository
{
    Task<Rating?> GetByIdAsync(Id id, CancellationToken cancellationToken = default);

    /// <summary>True if the rater has already rated this announcement.</summary>
    Task<bool> ExistsAsync(Id announcementId, UserId raterId, CancellationToken cancellationToken = default);

    Task AddAsync(Rating rating, CancellationToken cancellationToken = default);
}
