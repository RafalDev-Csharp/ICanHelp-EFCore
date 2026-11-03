using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Repositories;

public interface ILikedUserRepository
{
    Task<LikedUser?> GetAsync(UserId likerId, UserId likedId, CancellationToken cancellationToken = default);

    Task AddAsync(LikedUser likedUser, CancellationToken cancellationToken = default);

    void Remove(LikedUser likedUser);
}
