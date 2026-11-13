namespace ICanHelp.Application.DTO;

/// <summary>
/// One notification of the reader. <see cref="Title"/> and <see cref="Body"/> are ready texts in Polish (Krok 15,
/// the same as in the push message); <see cref="Type"/> + <see cref="Parameters"/> stay for icons and own formatting.
/// </summary>
public sealed record NotificationDto
{
    public Guid Id { get; init; }

    /// <summary>E.g. „Nowa oferta: 120 zł”.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>E.g. the announcement title or the beginning of the message (may be empty).</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>Path in the app that a click opens, e.g. <c>/conversations/{id}</c>; null = nothing to open.</summary>
    public string? Url { get; init; }

    /// <summary>
    /// OfferReceived, OfferCountered, OfferAccepted, OfferRejected, OfferWithdrawn, MessageReceived, AnnouncementEdited,
    /// NewAnnouncementNearby (alert: announcementTitle, categoryName, alertName, alertKind, distanceKm),
    /// NewAnnouncementsDigest (over the daily limit: count), OfferRevised (price, proposedBy).
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Text parameters, e.g. announcementTitle, price, messagePreview, changedFields, distanceKm, count.</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();

    public Guid? AnnouncementId { get; init; }
    public Guid? ConversationId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ReadAt { get; init; }
    public bool IsRead => ReadAt is not null;
}

public sealed record UnreadCountDto(int Count);
