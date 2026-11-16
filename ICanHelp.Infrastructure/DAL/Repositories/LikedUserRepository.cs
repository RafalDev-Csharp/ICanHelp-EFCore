using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Repositories;

internal sealed class LikedUserRepository(ICanHelpDbContext db) : ILikedUserRepository
{
    public Task<LikedUser?> GetAsync(UserId likerId, UserId likedId, CancellationToken cancellationToken = default)
        => db.LikedUsers.SingleOrDefaultAsync(l => l.LikerId == likerId && l.LikedId == likedId, cancellationToken);

    public async Task AddAsync(LikedUser likedUser, CancellationToken cancellationToken = default)
        => await db.LikedUsers.AddAsync(likedUser, cancellationToken);

    public void Remove(LikedUser likedUser) => db.LikedUsers.Remove(likedUser);
}
