namespace ICanHelp.Application.DTO;

public sealed record OfferDto
{
    public Guid Id { get; init; }
    public Guid AnnouncementId { get; init; }
    public Guid HelperId { get; init; }
    public string ProposedBy { get; init; } = string.Empty;
    public PriceDto Price { get; init; } = new();
    public DateTime ProposedStart { get; init; }
    public DateTime ProposedEnd { get; init; }
    public string? Message { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public Guid? ParentOfferId { get; init; }

    /// <summary>Revision of the announcement the offer was made on.</summary>
    public int AnnouncementRevision { get; init; }

    /// <summary>
    /// True if the announcement was edited after this offer (show a warning). Such an offer cannot be accepted –
    /// make a counter-offer instead.
    /// </summary>
    public bool IsOutdated { get; init; }

    /// <summary>
    /// How many times the announcement was edited after this offer – also edits that were reverted later
    /// (e.g. "⚠ edited 2 times after your offer"). Full history: <c>AnnouncementDetailsDto.Changes</c>.
    /// </summary>
    public int EditsSinceOffer { get; init; }

    /// <summary>
    /// What is different now compared to the moment of the offer, per field: <c>OldValue</c> = what the author of
    /// the offer saw, <c>NewValue</c> = what the announcement says now. Several edits are merged, so a later edit
    /// cannot hide an earlier one. Empty if the content is the same as then (even if <see cref="IsOutdated"/>).
    /// </summary>
    public IReadOnlyList<AnnouncementFieldChangeDto> ChangesSinceOffer { get; init; } = [];
}
