using System.Security.Cryptography;
using System.Text;
using ICanHelp.Application.Security;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace ICanHelp.Infrastructure.Security;

/// <summary>
/// 32 losowe bajty (Base64Url) – nie da się ich zgadnąć, więc wystarczy szybki SHA-256 bez soli
/// (sól i wolny hash są potrzebne dla haseł wymyślanych przez ludzi).
/// </summary>
internal sealed class RefreshTokenService : IRefreshTokenService
{
    private const int TokenBytes = 32;

    public RefreshTokenService(IOptions<AuthOptions> options)
    {
        Lifetime = options.Value.RefreshTokenExpiry;
    }

    public TimeSpan Lifetime { get; }

    public GeneratedRefreshToken Generate()
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));
        return new GeneratedRefreshToken(token, Hash(token));
    }

    public string Hash(string token)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
