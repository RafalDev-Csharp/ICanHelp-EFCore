using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Queries;

/// <summary>Publiczny profil użytkownika (bez danych kontaktowych).</summary>
public sealed class GetUserPublicById : IQuery<UserPublicDto?>
{
    public Guid UserId { get; init; }
}
