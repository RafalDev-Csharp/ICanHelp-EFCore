using ICanHelp.Application.Abstractions;

namespace ICanHelp.Application.Commands;

/// <summary>Logowanie e-mailem i hasłem. Wynik (JWT) trafia do <c>ITokenStorage</c>.</summary>
public sealed record SignIn : ICommand
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
