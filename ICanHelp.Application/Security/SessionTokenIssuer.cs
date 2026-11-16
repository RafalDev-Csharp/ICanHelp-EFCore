using ICanHelp.Application.DTO;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Security;

/// <summary>
/// Wspólne dla logowania i odświeżania: JWT + nowy refresh token zapisany (jako hash) w repozytorium.
/// Handlery znają tylko tę klasę – nie muszą same przekazywać generatora tokenów i wystawcy JWT.
/// </summary>
internal sealed class SessionTokenIssuer(IRefreshTokenRepository refreshTokens, IRefreshTokenService refreshTokenService,
    IAuthenticator authenticator)
{
    /// <param name="user">Dla kogo jest nowa para tokenów.</param>
    /// <param name="nowUtc">Bieżący czas (z <c>IClock</c>).</param>
    /// <param name="cancellationToken">Anulowanie zapisu.</param>
    /// <param name="replaced">
    /// Token, którego właśnie użyto do odświeżenia – zostaje unieważniony i wskazuje na nowy (rotacja).
    /// </param>
    public async Task<JwtDto> IssueAsync(User user, DateTime nowUtc, CancellationToken cancellationToken,
        RefreshToken? replaced = null)
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
