using ICanHelp.Core.Entities;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Repositories;

public interface IAnnouncementRepository
{
    /// <summary>Loads the announcement together with its offers and images.</summary>
    Task<Announcement?> GetByIdAsync(Id id, CancellationToken cancellationToken = default);

    Task AddAsync(Announcement announcement, CancellationToken cancellationToken = default);

    void Remove(Announcement announcement);
}
