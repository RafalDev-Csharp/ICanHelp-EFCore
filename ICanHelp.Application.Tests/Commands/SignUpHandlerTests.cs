using ICanHelp.Application.Commands;
using ICanHelp.Application.Commands.Handlers;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Tests.Fakes;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;

namespace ICanHelp.Application.Tests.Commands;

public class SignUpHandlerTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FixedClock _clock = new();
    private readonly SignUpHandler _handler;

    public SignUpHandlerTests()
    {
        _handler = new SignUpHandler(_users, _unitOfWork, new FakePasswordManager(), _clock);
    }

    internal static SignUp ValidCommand(string userName = "jan.kowalski", string email = "jan@example.com") => new()
    {
        UserId = Guid.NewGuid(),
        UserName = userName,
        FullName = "Jan Kowalski",
        Email = email,
        Password = "Haslo123!",
        PhoneNumber = "+48600100200",
        Country = "Polska",
        City = "Kraków",
        PostalCode = "30-001"
    };

    [Fact]
    public async Task SignUp_ValidData_AddsUserWithHashedPassword()
    {
        var command = ValidCommand() with { Description = "Złota rączka" };

        await _handler.HandleAsync(command);

        var user = _users.Users.ShouldHaveSingleItem();
        user.Id.Value.ShouldBe(command.UserId);
        user.UserName.Value.ShouldBe("jan.kowalski");
        user.ContactData.Email.Value.ShouldBe("jan@example.com");
        user.PasswordHash.ShouldBe(FakePasswordManager.Prefix + "Haslo123!");
        user.PasswordHash.ShouldNotBe(command.Password);
        user.Role.ShouldBe(UserRole.User);
        user.CreatedAt.ShouldBe(_clock.UtcNow);
        user.IsLocked.ShouldBeFalse();
        user.Description!.Value.ShouldBe("Złota rączka");
        _unitOfWork.Executions.ShouldBe(1);
    }

    [Fact]
    public async Task SignUp_WithoutDescription_LeavesDescriptionEmpty()
    {
        await _handler.HandleAsync(ValidCommand() with { Description = "  " });

        _users.Users.ShouldHaveSingleItem().Description.ShouldBeNull();
    }

    [Fact]
    public async Task SignUp_TakenUserName_Throws()
    {
        await _handler.HandleAsync(ValidCommand());

        await Should.ThrowAsync<UserNameAlreadyInUseException>(
            () => _handler.HandleAsync(ValidCommand(email: "inny@example.com")));
        _users.Users.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SignUp_TakenEmail_IgnoringCase_Throws()
    {
        await _handler.HandleAsync(ValidCommand());

        await Should.ThrowAsync<EmailAlreadyInUseException>(
            () => _handler.HandleAsync(ValidCommand(userName: "inny.user", email: "JAN@example.com")));
        _users.Users.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    public async Task SignUp_TooShortPassword_Throws(string password)
    {
        await Should.ThrowAsync<InvalidPasswordException>(
            () => _handler.HandleAsync(ValidCommand() with { Password = password }));
        _users.Users.ShouldBeEmpty();
    }

    [Fact]
    public async Task SignUp_InvalidEmail_Throws()
    {
        await Should.ThrowAsync<InvalidEmailException>(
            () => _handler.HandleAsync(ValidCommand(email: "not-an-email")));
        _users.Users.ShouldBeEmpty();
    }
}
