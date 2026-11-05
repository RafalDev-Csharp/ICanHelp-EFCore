using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Security;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

internal sealed class SignInHandler : ICommandHandler<SignIn>
{
    private readonly IUserRepository _users;
    private readonly IPasswordManager _passwordManager;
    private readonly IAuthenticator _authenticator;
    private readonly ITokenStorage _tokenStorage;

    public SignInHandler(IUserRepository users, IPasswordManager passwordManager, IAuthenticator authenticator,
        ITokenStorage tokenStorage)
    {
        _users = users;
        _passwordManager = passwordManager;
        _authenticator = authenticator;
        _tokenStorage = tokenStorage;
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

        var jwt = _authenticator.CreateToken(user.Id, user.UserName, user.Role);
        _tokenStorage.Set(jwt);
    }
}
