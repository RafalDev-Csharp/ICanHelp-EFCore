namespace ICanHelp.Application.DTO;

/// <summary>Token dostępu zwracany po zalogowaniu. Klient wysyła go w nagłówku <c>Authorization: Bearer ...</c>.</summary>
public sealed record JwtDto
{
    public string AccessToken { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
}
