using ICanHelp.Core.Entities;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Repositories;

internal sealed class AnnouncementRepository(ICanHelpDbContext db) : IAnnouncementRepository
{
    // The whole aggregate is loaded (offers + images + edit history + job exits), so its rules can be checked in memory.
    public Task<Announcement?> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
        => db.Announcements
            .Include(a => a.Offers)
            .Include(a => a.Images)
            .Include(a => a.Changes)
            .Include(a => a.JobExits)
            .AsSplitQuery()
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task AddAsync(Announcement announcement, CancellationToken cancellationToken = default)
        => await db.Announcements.AddAsync(announcement, cancellationToken);

    public void Remove(Announcement announcement) => db.Announcements.Remove(announcement);
}
