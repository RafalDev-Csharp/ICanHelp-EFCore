using ICanHelp.Application.Commands;
using ICanHelp.Application.Commands.Handlers;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Tests.Fakes;
using ICanHelp.Core.Enums;

namespace ICanHelp.Application.Tests.Commands;

public class SignInHandlerTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly FakeAuthenticator _authenticator = new();
    private readonly FakeTokenStorage _tokenStorage = new();
    private readonly SignInHandler _handler;
    private readonly SignUp _registered = SignUpHandlerTests.ValidCommand();

    public SignInHandlerTests()
    {
        var passwordManager = new FakePasswordManager();
        _handler = new SignInHandler(_users, passwordManager, _authenticator, _tokenStorage);

        // Rejestrujemy użytkownika prawdziwym handlerem, żeby nie budować encji ręcznie.
        new SignUpHandler(_users, new FakeUnitOfWork(), passwordManager, new FixedClock())
            .HandleAsync(_registered).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task SignIn_ValidCredentials_StoresToken()
    {
        await _handler.HandleAsync(new SignIn { Email = "JAN@example.com", Password = _registered.Password });

        _tokenStorage.Get().ShouldNotBeNull().AccessToken.ShouldBe($"token-for-{_registered.UserId}");
        _authenticator.LastCall.ShouldBe((_registered.UserId, _registered.UserName, UserRole.User));
    }

    [Fact]
    public async Task SignIn_WrongPassword_ThrowsInvalidCredentials()
    {
        await Should.ThrowAsync<InvalidCredentialsException>(
            () => _handler.HandleAsync(new SignIn { Email = _registered.Email, Password = "zle-haslo" }));
        _tokenStorage.Get().ShouldBeNull();
    }

    [Fact]
    public async Task SignIn_UnknownEmail_ThrowsInvalidCredentials()
    {
        await Should.ThrowAsync<InvalidCredentialsException>(
            () => _handler.HandleAsync(new SignIn { Email = "nikt@example.com", Password = _registered.Password }));
    }

    [Theory]
    [InlineData("", "Haslo123!")]
    [InlineData("jan@example.com", "")]
    public async Task SignIn_MissingData_ThrowsInvalidCredentials(string email, string password)
    {
        await Should.ThrowAsync<InvalidCredentialsException>(
            () => _handler.HandleAsync(new SignIn { Email = email, Password = password }));
    }

    [Fact]
    public async Task SignIn_LockedUser_Throws()
    {
        _users.Users.Single().Lock();

        await Should.ThrowAsync<UserLockedException>(
            () => _handler.HandleAsync(new SignIn { Email = _registered.Email, Password = _registered.Password }));
        _tokenStorage.Get().ShouldBeNull();
    }

    [Fact]
    public async Task SignIn_LockedUser_WrongPassword_DoesNotRevealLock()
    {
        _users.Users.Single().Lock();

        await Should.ThrowAsync<InvalidCredentialsException>(
            () => _handler.HandleAsync(new SignIn { Email = _registered.Email, Password = "zle-haslo" }));
    }
}
