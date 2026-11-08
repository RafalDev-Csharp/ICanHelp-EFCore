using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Security;
using ICanHelp.Application.Time;
using ICanHelp.Core.Repositories;

namespace ICanHelp.Application.Commands.Handlers;

internal sealed class RefreshSessionHandler : ICommandHandler<RefreshSession>
{
    /// <summary>
    /// Ponowne użycie tokenu tuż po rotacji to zwykle wyścig (dwie karty odświeżają naraz) – wtedy tylko 401.
    /// Później oznacza wyciek: unieważniamy wszystkie sesje użytkownika.
    /// </summary>
    public static readonly TimeSpan ReuseGracePeriod = TimeSpan.FromMinutes(1);

    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAuthenticator _authenticator;
    private readonly ITokenStorage _tokenStorage;
    private readonly IClock _clock;

    public RefreshSessionHandler(IRefreshTokenRepository refreshTokens, IUserRepository users, IUnitOfWork unitOfWork,
        IRefreshTokenService refreshTokenService, IAuthenticator authenticator, ITokenStorage tokenStorage, IClock clock)
    {
        _refreshTokens = refreshTokens;
        _users = users;
        _unitOfWork = unitOfWork;
        _refreshTokenService = refreshTokenService;
        _authenticator = authenticator;
        _tokenStorage = tokenStorage;
        _clock = clock;
    }

    public async Task HandleAsync(RefreshSession command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            throw new InvalidRefreshTokenException();
        }

        var now = _clock.UtcNow;
        var hash = _refreshTokenService.Hash(command.RefreshToken);
        JwtDto? jwt = null;

        // Unieważnienie sesji po wykryciu wycieku musi się zapisać – dlatego wyjątek rzucamy dopiero po transakcji.
        await _unitOfWork.ExecuteAsync(async () =>
        {
            var token = await _refreshTokens.GetByHashAsync(hash, cancellationToken);
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
                    foreach (var active in await _refreshTokens.GetActiveAsync(token.UserId, now, cancellationToken))
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
            var user = await _users.GetActiveOrThrowAsync(token.UserId, cancellationToken);
            jwt = await _refreshTokens.IssueAsync(_refreshTokenService, _authenticator, user, now, cancellationToken,
                replaced: token);
        }, cancellationToken);

        if (jwt is null)
        {
            throw new InvalidRefreshTokenException();
        }

        _tokenStorage.Set(jwt);
    }
}

internal sealed class EndSessionHandler : ICommandHandler<EndSession>
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IClock _clock;

    public EndSessionHandler(IRefreshTokenRepository refreshTokens, IUnitOfWork unitOfWork,
        IRefreshTokenService refreshTokenService, IClock clock)
    {
        _refreshTokens = refreshTokens;
        _unitOfWork = unitOfWork;
        _refreshTokenService = refreshTokenService;
        _clock = clock;
    }

    public async Task HandleAsync(EndSession command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return;
        }

        var hash = _refreshTokenService.Hash(command.RefreshToken);

        await _unitOfWork.ExecuteAsync(async () =>
        {
            var token = await _refreshTokens.GetByHashAsync(hash, cancellationToken);
            token?.Revoke(_clock.UtcNow);
        }, cancellationToken);
    }
}
