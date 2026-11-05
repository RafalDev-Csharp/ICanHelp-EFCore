namespace ICanHelp.Application.DTO;

/// <summary>
/// Publiczny profil użytkownika – to, co widzi każdy (także niezalogowany).
/// Bez danych kontaktowych, roli i blokady: pełne dane (<see cref="UserDto"/>) widzi tylko właściciel konta i admin.
/// </summary>
public sealed record UserPublicDto
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string City { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
