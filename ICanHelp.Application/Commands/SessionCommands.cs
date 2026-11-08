using ICanHelp.Application.Abstractions;

namespace ICanHelp.Application.Commands;

/// <summary>
/// Nowa para tokenów za refresh token (bez hasła). Stary token przestaje działać (rotacja).
/// Wynik (JWT) trafia do <c>ITokenStorage</c>.
/// </summary>
public sealed record RefreshSession : ICommand
{
    public string RefreshToken { get; init; } = string.Empty;
}

/// <summary>Wylogowanie: unieważnia refresh token. Nieznany albo już nieważny token – nic się nie dzieje.</summary>
public sealed record EndSession : ICommand
{
    public string RefreshToken { get; init; } = string.Empty;
}
