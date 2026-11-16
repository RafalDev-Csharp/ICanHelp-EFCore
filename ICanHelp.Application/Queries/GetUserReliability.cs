using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Queries;

/// <summary>F11 E: publiczne liczniki rezygnacji i przerwanych zleceń użytkownika (nieznany użytkownik → null → 404).</summary>
public sealed class GetUserReliability : IQuery<UserReliabilityDto?>
{
    public Guid UserId { get; init; }
}
