using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Repositories;

internal sealed class LikedAnnouncementRepository(ICanHelpDbContext db) : ILikedAnnouncementRepository
{
    public Task<LikedAnnouncement?> GetAsync(UserId userId, Id announcementId, CancellationToken cancellationToken = default)
        => db.LikedAnnouncements.SingleOrDefaultAsync(
            l => l.UserId == userId && l.AnnouncementId == announcementId, cancellationToken);

    public async Task AddAsync(LikedAnnouncement likedAnnouncement, CancellationToken cancellationToken = default)
        => await db.LikedAnnouncements.AddAsync(likedAnnouncement, cancellationToken);

    public void Remove(LikedAnnouncement likedAnnouncement) => db.LikedAnnouncements.Remove(likedAnnouncement);
}
