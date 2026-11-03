namespace ICanHelp.Application.DTO;

public sealed record LikedUserDto
{
    public Guid LikerId { get; init; }
    public Guid LikedId { get; init; }
    public DateTime LikedAt { get; init; }
}
