using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities.Liked;

/// <summary>
/// "User likes announcement". The row exists while the like exists, so unliking means deleting it.
/// The key is (UserId, AnnouncementId).
/// </summary>
public class LikedAnnouncement
{
    public UserId UserId { get; private set; }
    public Id AnnouncementId { get; private set; }
    public DateTime LikedAt { get; private set; }

    // Required by EF Core.
    private LikedAnnouncement()
    {
    }

    public LikedAnnouncement(UserId userId, Id announcementId, DateTime likedAtUtc)
    {
        UserId = userId;
        AnnouncementId = announcementId;
        LikedAt = likedAtUtc;
    }
}
