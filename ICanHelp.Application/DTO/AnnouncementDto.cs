namespace ICanHelp.Application.DTO;

/// <summary>An announcement on a list (without offers and images – only the thumbnail).</summary>
public record AnnouncementDto
{
    public Guid Id { get; init; }
    public Guid OwnerId { get; init; }
    public Guid CategoryId { get; init; }
    public Guid SubcategoryId { get; init; }
    public string Difficulty { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public PriceDto Budget { get; init; } = new();
    public LocationDto Location { get; init; } = new();
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public Guid? AcceptedOfferId { get; init; }

    /// <summary>0 = as published, +1 for every edit. Offers made on an older revision are outdated.</summary>
    public int Revision { get; init; }

    /// <summary>When the content was last edited (null if never).</summary>
    public DateTime? UpdatedAt { get; init; }

    /// <summary>When the accepted helper reported the work as done (status InProgress, waits for the owner's confirmation).</summary>
    public DateTime? DoneReportedAt { get; init; }

    /// <summary>URL of the first (oldest) image – the thumbnail on a list; null if there are no images.</summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>Distance from the search point in km – only when the list was searched by location.</summary>
    public double? DistanceKm { get; init; }

    /// <summary>How many helpers have made an offer (public – the offers themselves are not).</summary>
    public int HelperCount { get; init; }
}

/// <summary>An announcement with the negotiation and images (oldest first).</summary>
public sealed record AnnouncementDetailsDto : AnnouncementDto
{
    /// <summary>
    /// Offers visible to the reader: the owner sees all of them, a helper only his own, anybody else (also anonymous) none.
    /// </summary>
    public IReadOnlyList<OfferDto> Offers { get; init; } = [];

    /// <summary>Whether the signed-in reader has liked the announcement (always false for anonymous readers).</summary>
    public bool IsLiked { get; init; }

    public IReadOnlyList<ImageDto> Images { get; init; } = [];

    /// <summary>History of edits, oldest first.</summary>
    public IReadOnlyList<AnnouncementChangeDto> Changes { get; init; } = [];
}

/// <summary>One edit of an announcement: which revision it created and what exactly changed.</summary>
public sealed record AnnouncementChangeDto
{
    public int Revision { get; init; }
    public DateTime ChangedAt { get; init; }
    public IReadOnlyList<AnnouncementFieldChangeDto> Fields { get; init; } = [];
}

/// <summary>
/// One changed field with its value before and after – the UI highlights the difference
/// (for Title/Description best as a word diff). Formats: Budget "150.00 PLN", Location "50.0614, 19.9366",
/// Category "{categoryId}/{subcategoryId}", Difficulty "Easy".
/// </summary>
public sealed record AnnouncementFieldChangeDto
{
    /// <summary>Category, Difficulty, Title, Description, Budget, Location or Images.</summary>
    public string Field { get; init; } = string.Empty;
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
}
