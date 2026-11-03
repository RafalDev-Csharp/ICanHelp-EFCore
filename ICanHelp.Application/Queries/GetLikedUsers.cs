using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Queries;

public sealed class GetLikedUsers : IQuery<IReadOnlyList<LikedUserDto>>
{
    public Guid UserId { get; init; }
}
