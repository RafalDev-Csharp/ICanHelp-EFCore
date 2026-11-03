using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Repositories;

public interface ILikedAnnouncementRepository
{
    Task<LikedAnnouncement?> GetAsync(UserId userId, Id announcementId, CancellationToken cancellationToken = default);

    Task AddAsync(LikedAnnouncement likedAnnouncement, CancellationToken cancellationToken = default);

    void Remove(LikedAnnouncement likedAnnouncement);
}
