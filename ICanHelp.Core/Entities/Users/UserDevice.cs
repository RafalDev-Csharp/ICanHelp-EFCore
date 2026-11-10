using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities.Users;

/// <summary>
/// Aggregate root: a device (browser / phone) of the user that gets push notifications (Krok 15). <see cref="Token"/>
/// is the FCM registration token and is unique: the same token reported again only refreshes <see cref="LastSeenAt"/>
/// (and the owner, when somebody else signed in on that device). A user has at most <see cref="MaxPerUser"/> devices –
/// a new one pushes out the ones not seen for the longest time (<see cref="SelectToRemoveBeforeAdding"/>).
/// </summary>
public class UserDevice
{
    public const int MaxPerUser = 10;
    public const int MaxTokenLength = 512;

    public Id Id { get; private set; }
    public UserId UserId { get; private set; }
    public string Token { get; private set; }
    public DevicePlatform Platform { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>The last time the app reported this token (registration or refresh).</summary>
    public DateTime LastSeenAt { get; private set; }

    // Required by EF Core.
    private UserDevice()
    {
    }

    public UserDevice(Id id, UserId userId, string token, DevicePlatform platform, DateTime createdAtUtc)
    {
        Id = id;
        UserId = userId;
        Token = ValidateToken(token);
        Platform = ValidatePlatform(platform);
        CreatedAt = createdAtUtc;
        LastSeenAt = createdAtUtc;
    }

    public bool IsOwnedBy(UserId userId) => UserId == userId;

    /// <summary>
    /// The token was reported again: by the same user (app started again) or by another user who signed in on this
    /// device – then the device moves to that user, so the previous one no longer gets their pushes here.
    /// </summary>
    public void Refresh(UserId userId, DevicePlatform platform, DateTime seenAtUtc)
    {
        UserId = userId;
        Platform = ValidatePlatform(platform);

        if (seenAtUtc > LastSeenAt)
        {
            LastSeenAt = seenAtUtc;
        }
    }

    /// <summary>
    /// Which devices of the user must go so that one more fits under <see cref="MaxPerUser"/> – the ones not seen
    /// for the longest time.
    /// </summary>
    public static IReadOnlyList<UserDevice> SelectToRemoveBeforeAdding(IReadOnlyCollection<UserDevice> devicesOfUser)
    {
        var excess = devicesOfUser.Count - (MaxPerUser - 1);
        return excess <= 0
            ? []
            : devicesOfUser.OrderBy(d => d.LastSeenAt).ThenBy(d => d.CreatedAt).Take(excess).ToList();
    }

    private static string ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength || token.Any(char.IsWhiteSpace))
        {
            throw new InvalidDeviceException($"Device token must have 1–{MaxTokenLength} characters without spaces.");
        }

        return token;
    }

    private static DevicePlatform ValidatePlatform(DevicePlatform platform)
        => Enum.IsDefined(platform)
            ? platform
            : throw new InvalidDeviceException($"Device platform: {platform} is invalid.");
}
