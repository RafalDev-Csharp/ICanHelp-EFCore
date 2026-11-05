using ICanHelp.Core.Entities;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Repositories;

internal sealed class RatingRepository : IRatingRepository
{
    private readonly ICanHelpDbContext _db;

    public RatingRepository(ICanHelpDbContext db)
    {
        _db = db;
    }

    public Task<Rating?> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
        => _db.Ratings.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Id announcementId, UserId raterId, CancellationToken cancellationToken = default)
        => _db.Ratings.AnyAsync(r => r.AnnouncementId == announcementId && r.RaterId == raterId, cancellationToken);

    public async Task AddAsync(Rating rating, CancellationToken cancellationToken = default)
        => await _db.Ratings.AddAsync(rating, cancellationToken);
}
