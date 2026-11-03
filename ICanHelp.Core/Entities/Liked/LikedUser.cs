using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities.Liked;

/// <summary>
/// "User likes another user" (e.g. a helper worth coming back to). Unliking means deleting the row.
/// The key is (LikerId, LikedId).
/// </summary>
public class LikedUser
{
    public UserId LikerId { get; private set; }
    public UserId LikedId { get; private set; }
    public DateTime LikedAt { get; private set; }

    // Required by EF Core.
    private LikedUser()
    {
    }

    public LikedUser(UserId likerId, UserId likedId, DateTime likedAtUtc)
    {
        if (likerId == likedId)
        {
            throw new DomainRuleException("A user cannot like himself.");
        }

        LikerId = likerId;
        LikedId = likedId;
        LikedAt = likedAtUtc;
    }
}
