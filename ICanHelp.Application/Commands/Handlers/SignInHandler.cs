using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Security;
using ICanHelp.Application.Time;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

internal sealed class SignInHandler : ICommandHandler<SignIn>
{
    private readonly IUserRepository _users;
    private readonly IPasswordManager _passwordManager;
    private readonly IAuthenticator _authenticator;
    private readonly ITokenStorage _tokenStorage;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SignInHandler(IUserRepository users, IPasswordManager passwordManager, IAuthenticator authenticator,
        ITokenStorage tokenStorage, IRefreshTokenRepository refreshTokens, IRefreshTokenService refreshTokenService,
        IUnitOfWork unitOfWork, IClock clock)
    {
        _users = users;
        _passwordManager = passwordManager;
        _authenticator = authenticator;
        _tokenStorage = tokenStorage;
        _refreshTokens = refreshTokens;
        _refreshTokenService = refreshTokenService;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task HandleAsync(SignIn command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Password))
        {
            throw new InvalidCredentialsException();
        }

        var user = await _users.GetByEmailAsync(new Email(command.Email), cancellationToken);

        if (user is null || !_passwordManager.Validate(command.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        // Blokadę zdradzamy dopiero po poprawnym haśle.
        if (user.IsLocked)
        {
            throw new UserLockedException();
        }

        // Krok F2: oprócz JWT – refresh token (każde logowanie = osobna sesja, np. telefon i komputer).
        await _unitOfWork.ExecuteAsync(async () =>
        {
            var jwt = await _refreshTokens.IssueAsync(_refreshTokenService, _authenticator, user, _clock.UtcNow,
                cancellationToken);
            _tokenStorage.Set(jwt);
        }, cancellationToken);
    }
}
