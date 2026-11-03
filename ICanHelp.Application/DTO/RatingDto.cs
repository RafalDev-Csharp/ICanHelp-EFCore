namespace ICanHelp.Application.DTO;

public sealed record RatingDto
{
    public Guid Id { get; init; }
    public Guid AnnouncementId { get; init; }
    public Guid RaterId { get; init; }
    public Guid RateeId { get; init; }
    public int Rate { get; init; }
    public string? Comment { get; init; }
    public DateTime CreatedAt { get; init; }
}
