using ICanHelp.Core.Enums;

namespace ICanHelp.Application.DTO;

/// <summary>A rating the reader received, with the announcement and the role he had in that transaction.</summary>
public sealed record MyRatingDto
{
    public Guid Id { get; init; }
    public Guid AnnouncementId { get; init; }
    public string AnnouncementTitle { get; init; } = string.Empty;

    /// <summary>The reader's side in the rated transaction: Client (owner) or Helper.</summary>
    public string MyRole { get; init; } = string.Empty;

    /// <summary>Who gave the rating (public data only).</summary>
    public ConversationPartyDto Rater { get; init; } = new();

    public int Rate { get; init; }
    public string? Comment { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>Client when the reader owns the announcement, otherwise Helper (only participants are rated).</summary>
    public static string RoleOf(Guid ownerId, Guid readerId)
        => (ownerId == readerId ? OfferParty.Client : OfferParty.Helper).ToString();
}

/// <summary>A completed announcement in which the reader has not rated the other side yet.</summary>
public sealed record PendingRatingDto
{
    public Guid AnnouncementId { get; init; }
    public string AnnouncementTitle { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }

    /// <summary>The reader's side: Client (owner) or Helper (accepted helper).</summary>
    public string MyRole { get; init; } = string.Empty;

    /// <summary>The side to be rated.</summary>
    public ConversationPartyDto OtherParty { get; init; } = new();

    public DateTime CreatedAt { get; init; }
}
