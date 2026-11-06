namespace ICanHelp.Application.DTO;

/// <summary>
/// The conversation of the announcement owner (client) with one helper, as seen by one of them.
/// <see cref="Timeline"/> = messages, offers of this helper and edits of the announcement, oldest first.
/// </summary>
public sealed record ConversationDto
{
    public Guid Id { get; init; }
    public Guid AnnouncementId { get; init; }
    public string AnnouncementTitle { get; init; } = string.Empty;
    public string AnnouncementStatus { get; init; } = string.Empty;

    /// <summary>The current revision of the announcement (messages and offers made on an older one refer to an older text).</summary>
    public int AnnouncementRevision { get; init; }

    public Guid ClientId { get; init; }
    public Guid HelperId { get; init; }

    /// <summary>The side of the user who reads the conversation: Client (owner) or Helper.</summary>
    public string MyRole { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
    public DateTime? ClientLastReadAt { get; init; }
    public DateTime? HelperLastReadAt { get; init; }

    /// <summary>Messages of the other side written after the reader last marked the conversation as read.</summary>
    public int UnreadCount { get; init; }

    /// <summary>False when the announcement no longer allows messages here (cancelled, or another helper was accepted).</summary>
    public bool CanWrite { get; init; }

    public IReadOnlyList<ConversationEntryDto> Timeline { get; init; } = [];
}

/// <summary>
/// One entry of the timeline. Exactly one of <see cref="Message"/>, <see cref="Offer"/>, <see cref="Change"/> is set –
/// according to <see cref="Type"/>.
/// </summary>
public sealed record ConversationEntryDto
{
    public const string MessageType = "Message";
    public const string OfferType = "Offer";

    /// <summary>A "system" entry: the owner edited the announcement (warning sign in the conversation).</summary>
    public const string AnnouncementChangedType = "AnnouncementChanged";

    /// <summary>Message, Offer or AnnouncementChanged.</summary>
    public string Type { get; init; } = string.Empty;
    public DateTime At { get; init; }

    public MessageDto? Message { get; init; }
    public OfferDto? Offer { get; init; }
    public AnnouncementChangeDto? Change { get; init; }
}

public sealed record MessageDto
{
    public Guid Id { get; init; }
    public Guid SenderId { get; init; }

    /// <summary>Client (owner) or Helper.</summary>
    public string SenderRole { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;
    public DateTime SentAt { get; init; }

    /// <summary>Revision of the announcement when the message was written.</summary>
    public int AnnouncementRevision { get; init; }
}
