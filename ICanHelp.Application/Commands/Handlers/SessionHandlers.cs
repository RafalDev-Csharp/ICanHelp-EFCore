using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Security;
using ICanHelp.Application.Time;
using ICanHelp.Core.Repositories;

namespace ICanHelp.Application.Commands.Handlers;

internal sealed class RefreshSessionHandler(IRefreshTokenRepository refreshTokens, IUserRepository users,
    IUnitOfWork unitOfWork, IRefreshTokenService refreshTokenService, SessionTokenIssuer tokenIssuer,
    ITokenStorage tokenStorage, IClock clock) : ICommandHandler<RefreshSession>
{
    /// <summary>
    /// Ponowne użycie tokenu tuż po rotacji to zwykle wyścig (dwie karty odświeżają naraz) – wtedy tylko 401.
    /// Później oznacza wyciek: unieważniamy wszystkie sesje użytkownika.
    /// </summary>
    public static readonly TimeSpan ReuseGracePeriod = TimeSpan.FromMinutes(1);

    public async Task HandleAsync(RefreshSession command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            throw new InvalidRefreshTokenException();
        }

        var now = clock.UtcNow;
        var hash = refreshTokenService.Hash(command.RefreshToken);
        JwtDto? jwt = null;

        // Unieważnienie sesji po wykryciu wycieku musi się zapisać – dlatego wyjątek rzucamy dopiero po transakcji.
        await unitOfWork.ExecuteAsync(async () =>
        {
            var token = await refreshTokens.GetByHashAsync(hash, cancellationToken);
            if (token is null)
            {
                return;
            }

            if (token.IsRevoked)
            {
                var reusedAfterRotation = token.ReplacedByTokenId is not null
                                          && now - token.RevokedAt!.Value > ReuseGracePeriod;
                if (reusedAfterRotation)
                {
                    foreach (var active in await refreshTokens.GetActiveAsync(token.UserId, now, cancellationToken))
                    {
                        active.Revoke(now);
                    }
                }

                return;
            }

            if (!token.IsActive(now))
            {
                return;
            }

            // Zablokowany użytkownik → 403, usunięty → 404 (transakcja się wycofuje, token zostaje bez zmian).
            var user = await users.GetActiveOrThrowAsync(token.UserId, cancellationToken);
            jwt = await tokenIssuer.IssueAsync(user, now, cancellationToken, replaced: token);
        }, cancellationToken);

        if (jwt is null)
        {
            throw new InvalidRefreshTokenException();
        }

        tokenStorage.Set(jwt);
    }
}

internal sealed class EndSessionHandler(IRefreshTokenRepository refreshTokens, IUnitOfWork unitOfWork,
    IRefreshTokenService refreshTokenService, IClock clock) : ICommandHandler<EndSession>
{
    public async Task HandleAsync(EndSession command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return;
        }

        var hash = refreshTokenService.Hash(command.RefreshToken);

        await unitOfWork.ExecuteAsync(async () =>
        {
            var token = await refreshTokens.GetByHashAsync(hash, cancellationToken);
            token?.Revoke(clock.UtcNow);
        }, cancellationToken);
    }
}
