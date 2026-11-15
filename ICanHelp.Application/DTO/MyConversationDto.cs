namespace ICanHelp.Application.DTO;

/// <summary>
/// One row of "my negotiations": a conversation with a preview of the announcement, the last offer and the last message,
/// as seen by the reader (Client = owner of the announcement, Helper). Newest activity first.
/// </summary>
public sealed record MyConversationDto
{
    public Guid ConversationId { get; init; }
    public Guid AnnouncementId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public string AnnouncementStatus { get; init; } = string.Empty;

    /// <summary>The reader's side: Client (owner) or Helper.</summary>
    public string MyRole { get; init; } = string.Empty;

    public ConversationPartyDto OtherParty { get; init; } = new();

    /// <summary>The newest offer in this conversation (also a counter-offer of the owner); null = no offer yet.</summary>
    public LastOfferDto? LastOffer { get; init; }

    /// <summary>The beginning of the newest message of either side (<c>TextPreview</c>); null = no messages yet.</summary>
    public string? LastMessagePreview { get; init; }

    /// <summary>
    /// The newest of: conversation started, last message, last offer, last edit of the announcement.
    /// The list is sorted by it (descending).
    /// </summary>
    public DateTime LastActivityAt { get; init; }

    /// <summary>Messages of the other side written after the reader last read the conversation.</summary>
    public int UnreadCount { get; init; }

    /// <summary>
    /// The owner edited the announcement after the reader last read the conversation (or, if he never read it,
    /// after it started) – show a warning sign. Only for the helper; always false for the owner (he made the edit).
    /// </summary>
    public bool AnnouncementChangedSinceLastRead { get; init; }
}

/// <summary>The other participant of the conversation (public data only).</summary>
public sealed record ConversationPartyDto
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
}

/// <summary>Short info about the newest offer – details in <c>GET api/conversations/{id}</c>.</summary>
public sealed record LastOfferDto
{
    public Guid Id { get; init; }
    public PriceDto Price { get; init; } = new();

    /// <summary>Pending, Accepted, Rejected, Withdrawn, Countered or Revised.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Client (counter-offer of the owner) or Helper.</summary>
    public string ProposedBy { get; init; } = string.Empty;

    /// <summary>The announcement was edited after this offer – it cannot be accepted, only countered.</summary>
    public bool IsOutdated { get; init; }

    public DateTime CreatedAt { get; init; }
}
