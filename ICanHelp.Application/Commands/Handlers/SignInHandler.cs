using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Security;
using ICanHelp.Application.Time;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

internal sealed class SignInHandler(IUserRepository users, IPasswordManager passwordManager,
    SessionTokenIssuer tokenIssuer, ITokenStorage tokenStorage, IUnitOfWork unitOfWork, IClock clock)
    : ICommandHandler<SignIn>
{
    public async Task HandleAsync(SignIn command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Password))
        {
            throw new InvalidCredentialsException();
        }

        var user = await users.GetByEmailAsync(new Email(command.Email), cancellationToken);

        if (user is null || !passwordManager.Validate(command.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        // Blokadę zdradzamy dopiero po poprawnym haśle.
        if (user.IsLocked)
        {
            throw new UserLockedException();
        }

        // Krok F2: oprócz JWT – refresh token (każde logowanie = osobna sesja, np. telefon i komputer).
        await unitOfWork.ExecuteAsync(async () =>
        {
            var jwt = await tokenIssuer.IssueAsync(user, clock.UtcNow, cancellationToken);
            tokenStorage.Set(jwt);
        }, cancellationToken);
    }
}
