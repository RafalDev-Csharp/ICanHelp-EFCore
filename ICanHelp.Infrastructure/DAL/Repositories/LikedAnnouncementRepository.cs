using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Repositories;

internal sealed class LikedAnnouncementRepository : ILikedAnnouncementRepository
{
    private readonly ICanHelpDbContext _db;

    public LikedAnnouncementRepository(ICanHelpDbContext db)
    {
        _db = db;
    }

    public Task<LikedAnnouncement?> GetAsync(UserId userId, Id announcementId, CancellationToken cancellationToken = default)
        => _db.LikedAnnouncements.SingleOrDefaultAsync(
            l => l.UserId == userId && l.AnnouncementId == announcementId, cancellationToken);

    public async Task AddAsync(LikedAnnouncement likedAnnouncement, CancellationToken cancellationToken = default)
        => await _db.LikedAnnouncements.AddAsync(likedAnnouncement, cancellationToken);

    public void Remove(LikedAnnouncement likedAnnouncement) => _db.LikedAnnouncements.Remove(likedAnnouncement);
}
