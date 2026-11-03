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
}
