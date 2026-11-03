using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Queries;

public sealed class GetLikedAnnouncements : IQuery<IReadOnlyList<LikedAnnouncementDto>>
{
    public Guid UserId { get; init; }
}
