namespace ICanHelp.Application.DTO;

/// <summary>
/// One notification of the reader. The client builds the text from <see cref="Type"/> and <see cref="Parameters"/>
/// (e.g. OfferReceived + announcementTitle + price → "Nowa oferta 120.00 PLN: Cieknący kran").
/// </summary>
public sealed record NotificationDto
{
    public Guid Id { get; init; }

    /// <summary>OfferReceived, OfferCountered, OfferAccepted, OfferRejected, OfferWithdrawn, MessageReceived, AnnouncementEdited.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Text parameters, e.g. announcementTitle, price, messagePreview, changedFields.</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();

    public Guid? AnnouncementId { get; init; }
    public Guid? ConversationId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ReadAt { get; init; }
    public bool IsRead => ReadAt is not null;
}

public sealed record UnreadCountDto(int Count);
