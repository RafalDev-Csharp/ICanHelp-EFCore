using ICanHelp.Core.Exceptions;

namespace ICanHelp.Application.Exceptions;

/// <summary>Refresh token nieznany, wygasły, unieważniony albo już użyty → 401 (trzeba zalogować się hasłem).</summary>
public class InvalidRefreshTokenException : CustomExceptions
{
    public InvalidRefreshTokenException() : base("Invalid refresh token.")
    {
    }
}
