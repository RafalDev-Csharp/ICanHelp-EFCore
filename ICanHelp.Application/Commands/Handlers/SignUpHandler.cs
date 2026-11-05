using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Security;
using ICanHelp.Application.Time;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

internal sealed class SignUpHandler : ICommandHandler<SignUp>
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordManager _passwordManager;
    private readonly IClock _clock;

    public SignUpHandler(IUserRepository users, IUnitOfWork unitOfWork, IPasswordManager passwordManager, IClock clock)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _passwordManager = passwordManager;
        _clock = clock;
    }

    public async Task HandleAsync(SignUp command, CancellationToken cancellationToken = default)
    {
        // Value objects walidują dane wejściowe (błędne dane -> wyjątek -> 400).
        var userId = new UserId(command.UserId);
        var userName = new UserName(command.UserName);
        var fullName = new FullName(command.FullName);
        var email = new Email(command.Email);
        var password = new Password(command.Password);
        var contactData = new ContactData(email, new PhoneNumber(command.PhoneNumber),
            command.Country, command.City, command.PostalCode);
        var description = string.IsNullOrWhiteSpace(command.Description) ? null : new Description(command.Description);

        await _unitOfWork.ExecuteAsync(async () =>
        {
            if (await _users.ExistsByUserNameAsync(userName, cancellationToken))
            {
                throw new UserNameAlreadyInUseException(userName);
            }

            if (await _users.ExistsByEmailAsync(email, cancellationToken))
            {
                throw new EmailAlreadyInUseException(email);
            }

            var user = new User(userId, userName, fullName, _passwordManager.Secure(password), contactData, _clock.UtcNow);
            if (description is not null)
            {
                user.UpdateProfile(fullName, description, contactData);
            }

            await _users.AddAsync(user, cancellationToken);
        }, cancellationToken);
    }
}
