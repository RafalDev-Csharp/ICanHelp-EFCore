namespace ICanHelp.Application.DTO;

public sealed record LikedAnnouncementDto
{
    public Guid UserId { get; init; }
    public Guid AnnouncementId { get; init; }
    public DateTime LikedAt { get; init; }
}
