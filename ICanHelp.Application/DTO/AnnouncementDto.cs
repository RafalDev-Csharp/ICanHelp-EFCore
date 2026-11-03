namespace ICanHelp.Application.DTO;

/// <summary>An announcement on a list (without offers and images).</summary>
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
}

/// <summary>An announcement with the whole negotiation and images.</summary>
public sealed record AnnouncementDetailsDto : AnnouncementDto
{
    public IReadOnlyList<OfferDto> Offers { get; init; } = [];
    public IReadOnlyList<ImageDto> Images { get; init; } = [];
}
