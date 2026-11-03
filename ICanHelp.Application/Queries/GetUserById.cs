using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Queries;

public sealed class GetUserById : IQuery<UserDto?>
{
    public Guid UserId { get; init; }
}
