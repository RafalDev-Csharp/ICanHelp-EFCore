using ICanHelp.Application.DTO;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Security;

/// <summary>Wspólne dla logowania i odświeżania: JWT + nowy refresh token zapisany (jako hash) w repozytorium.</summary>
internal static class TokenIssuer
{
    public static async Task<JwtDto> IssueAsync(this IRefreshTokenRepository refreshTokens,
        IRefreshTokenService refreshTokenService, IAuthenticator authenticator, User user, DateTime nowUtc,
        CancellationToken cancellationToken, RefreshToken? replaced = null)
    {
        var generated = refreshTokenService.Generate();
        var refreshToken = new RefreshToken(Id.New(), user.Id, generated.Hash, nowUtc,
            nowUtc.Add(refreshTokenService.Lifetime));

        replaced?.Rotate(refreshToken.Id, nowUtc);
        await refreshTokens.AddAsync(refreshToken, cancellationToken);

        return authenticator.CreateToken(user.Id, user.UserName, user.Role) with
        {
            RefreshToken = generated.Token,
            RefreshTokenExpiresAtUtc = refreshToken.ExpiresAt
        };
    }
}
