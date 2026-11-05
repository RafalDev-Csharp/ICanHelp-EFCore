using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Queries;

public sealed class GetUsers : IQuery<IReadOnlyList<UserDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
