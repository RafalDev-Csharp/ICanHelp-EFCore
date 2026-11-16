using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ICanHelp.Infrastructure.Security;

/// <summary>Sekcja <c>Auth</c> w appsettings. Klucz na produkcji podawaj zmienną <c>Auth__SigningKey</c>.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    /// <summary>Klucz HMAC-SHA256, min. 32 bajty.</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Ważność tokenu dostępu (JWT).</summary>
    public TimeSpan Expiry { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Ważność refresh tokenu (Krok F2). Każde użycie daje nowy, więc aktywny użytkownik nie musi się logować.</summary>
    public TimeSpan RefreshTokenExpiry { get; set; } = TimeSpan.FromDays(30);

    internal SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
