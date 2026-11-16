namespace ICanHelp.Application.DTO;

/// <summary>
/// Tokeny zwracane po zalogowaniu i odświeżeniu. <see cref="AccessToken"/> klient wysyła w nagłówku
/// <c>Authorization: Bearer ...</c>, a gdy wygaśnie – <see cref="RefreshToken"/> do <c>POST api/users/refresh</c>
/// (jednorazowy: w odpowiedzi przychodzi nowa para).
/// </summary>
public sealed record JwtDto
{
    public string AccessToken { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
    public string RefreshToken { get; init; } = string.Empty;
    public DateTime RefreshTokenExpiresAtUtc { get; init; }
}
