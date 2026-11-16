namespace ICanHelp.Application.Security;

/// <summary>Losowy refresh token i jego hash (w bazie trzymamy tylko hash). Implementacja w Infrastructure.</summary>
public interface IRefreshTokenService
{
    /// <summary>Ważność nowego tokenu (<c>Auth:RefreshTokenExpiry</c>, domyślnie 30 dni).</summary>
    TimeSpan Lifetime { get; }

    /// <summary>Nowy token (do oddania klientowi) i jego hash (do zapisania).</summary>
    GeneratedRefreshToken Generate();

    string Hash(string token);
}

public sealed record GeneratedRefreshToken(string Token, string Hash);
