using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Repositories;

internal sealed class LikedUserRepository : ILikedUserRepository
{
    private readonly ICanHelpDbContext _db;

    public LikedUserRepository(ICanHelpDbContext db)
    {
        _db = db;
    }

    public Task<LikedUser?> GetAsync(UserId likerId, UserId likedId, CancellationToken cancellationToken = default)
        => _db.LikedUsers.SingleOrDefaultAsync(l => l.LikerId == likerId && l.LikedId == likedId, cancellationToken);

    public async Task AddAsync(LikedUser likedUser, CancellationToken cancellationToken = default)
        => await _db.LikedUsers.AddAsync(likedUser, cancellationToken);

    public void Remove(LikedUser likedUser) => _db.LikedUsers.Remove(likedUser);
}
