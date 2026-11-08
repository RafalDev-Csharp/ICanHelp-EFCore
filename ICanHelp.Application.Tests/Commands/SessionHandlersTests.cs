using ICanHelp.Application.Commands;
using ICanHelp.Application.Commands.Handlers;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Tests.Fakes;

namespace ICanHelp.Application.Tests.Commands;

/// <summary>Krok F2: odświeżanie sesji (rotacja refresh tokenu, wykrycie ponownego użycia) i wylogowanie.</summary>
public class SessionHandlersTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryRefreshTokenRepository _refreshTokens = new();
    private readonly FakeRefreshTokenService _refreshTokenService = new();
    private readonly FakeTokenStorage _tokenStorage = new();
    private readonly MutableClock _clock = new();
    private readonly SignUp _registered = SignUpHandlerTests.ValidCommand();

    public SessionHandlersTests()
    {
        var passwordManager = new FakePasswordManager();
        new SignUpHandler(_users, new FakeUnitOfWork(), passwordManager, new FixedClock())
            .HandleAsync(_registered).GetAwaiter().GetResult();
    }

    private RefreshSessionHandler RefreshHandler(FakeTokenStorage? storage = null)
        => new(_refreshTokens, _users, new FakeUnitOfWork(), _refreshTokenService, new FakeAuthenticator(),
            storage ?? _tokenStorage, _clock);

    private EndSessionHandler EndHandler()
        => new(_refreshTokens, new FakeUnitOfWork(), _refreshTokenService, _clock);

    /// <summary>Logowanie prawdziwym handlerem – zwraca refresh token.</summary>
    private async Task<string> SignInAsync()
    {
        var storage = new FakeTokenStorage();
        await new SignInHandler(_users, new FakePasswordManager(), new FakeAuthenticator(), storage, _refreshTokens,
                _refreshTokenService, new FakeUnitOfWork(), _clock)
            .HandleAsync(new SignIn { Email = _registered.Email, Password = _registered.Password });
        return storage.Get()!.RefreshToken;
    }

    private Task RefreshAsync(string refreshToken)
        => RefreshHandler().HandleAsync(new RefreshSession { RefreshToken = refreshToken });

    [Fact]
    public async Task Refresh_ValidToken_IssuesNewPair_AndRotatesOldToken()
    {
        var old = await SignInAsync();
        _clock.UtcNow = _clock.UtcNow.AddHours(2);

        await RefreshAsync(old);

        var jwt = _tokenStorage.Get().ShouldNotBeNull();
        jwt.AccessToken.ShouldBe($"token-for-{_registered.UserId}");
        jwt.RefreshToken.ShouldNotBe(old);
        jwt.RefreshTokenExpiresAtUtc.ShouldBe(_clock.UtcNow.AddDays(30));

        var oldToken = _refreshTokens.Tokens.Single(t => t.TokenHash == _refreshTokenService.Hash(old));
        var newToken = _refreshTokens.Tokens.Single(t => t.TokenHash == _refreshTokenService.Hash(jwt.RefreshToken));
        oldToken.RevokedAt.ShouldBe(_clock.UtcNow);
        oldToken.ReplacedByTokenId.ShouldBe(newToken.Id);
        newToken.IsActive(_clock.UtcNow).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("nieznany-token")]
    public async Task Refresh_UnknownToken_Throws(string refreshToken)
    {
        await Should.ThrowAsync<InvalidRefreshTokenException>(() => RefreshAsync(refreshToken));
        _tokenStorage.Get().ShouldBeNull();
    }

    [Fact]
    public async Task Refresh_ExpiredToken_Throws()
    {
        var token = await SignInAsync();
        _clock.UtcNow = _clock.UtcNow.AddDays(31);

        await Should.ThrowAsync<InvalidRefreshTokenException>(() => RefreshAsync(token));
    }

    [Fact]
    public async Task Refresh_ReusedRightAfterRotation_Throws_ButKeepsNewSession()
    {
        var old = await SignInAsync();
        await RefreshAsync(old);
        var current = _tokenStorage.Get()!.RefreshToken;

        // Druga karta z tym samym tokenem kilka sekund później – wyścig, nie wyciek.
        _clock.UtcNow = _clock.UtcNow.AddSeconds(5);
        await Should.ThrowAsync<InvalidRefreshTokenException>(() => RefreshAsync(old));

        _refreshTokens.Tokens.Single(t => t.TokenHash == _refreshTokenService.Hash(current))
            .IsActive(_clock.UtcNow).ShouldBeTrue();
    }

    [Fact]
    public async Task Refresh_ReusedLaterAfterRotation_RevokesAllSessionsOfUser()
    {
        var old = await SignInAsync();
        var otherDevice = await SignInAsync();
        await RefreshAsync(old);
        var current = _tokenStorage.Get()!.RefreshToken;

        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await Should.ThrowAsync<InvalidRefreshTokenException>(() => RefreshAsync(old));

        _refreshTokens.Tokens.ShouldAllBe(t => !t.IsActive(_clock.UtcNow));
        await Should.ThrowAsync<InvalidRefreshTokenException>(() => RefreshAsync(current));
        await Should.ThrowAsync<InvalidRefreshTokenException>(() => RefreshAsync(otherDevice));
    }

    [Fact]
    public async Task Refresh_LockedUser_ThrowsUserLocked()
    {
        var token = await SignInAsync();
        _users.Users.Single().Lock();

        await Should.ThrowAsync<UserLockedException>(() => RefreshAsync(token));
        _tokenStorage.Get().ShouldBeNull();
    }

    [Fact]
    public async Task EndSession_RevokesToken_SoRefreshFails()
    {
        var token = await SignInAsync();

        await EndHandler().HandleAsync(new EndSession { RefreshToken = token });

        _refreshTokens.Tokens.Single().IsRevoked.ShouldBeTrue();
        await Should.ThrowAsync<InvalidRefreshTokenException>(() => RefreshAsync(token));
    }

    [Fact]
    public async Task EndSession_OnlyThatSession_OtherDeviceStaysSignedIn()
    {
        var phone = await SignInAsync();
        var laptop = await SignInAsync();

        await EndHandler().HandleAsync(new EndSession { RefreshToken = phone });

        await RefreshAsync(laptop);
        _tokenStorage.Get().ShouldNotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("nieznany-token")]
    public async Task EndSession_UnknownToken_DoesNothing(string refreshToken)
        => await Should.NotThrowAsync(() => EndHandler().HandleAsync(new EndSession { RefreshToken = refreshToken }));
}
