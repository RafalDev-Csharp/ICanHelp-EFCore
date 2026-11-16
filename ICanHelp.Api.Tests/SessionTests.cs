using System.Net.Http.Headers;
using ICanHelp.Api.Tests.Infrastructure;
using ICanHelp.Application.DTO;

namespace ICanHelp.Api.Tests;

/// <summary>Krok F2: refresh token – logowanie, odświeżenie (rotacja), wylogowanie.</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public sealed class SessionTests
{
    private readonly ApiFactory _factory;

    public SessionTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<JwtDto> SignInAsync(TestUser user)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/users/sign-in",
            new { email = user.Email, password = user.Password });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadAsync<JwtDto>();
    }

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken)
        => _factory.CreateClient().PostAsJsonAsync("/api/users/refresh", new { refreshToken });

    [Fact]
    public async Task SignIn_ReturnsRefreshToken()
    {
        var user = await _factory.CreateUserAsync();

        var jwt = await SignInAsync(user);

        jwt.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        jwt.RefreshTokenExpiresAtUtc.ShouldBeGreaterThan(jwt.ExpiresAtUtc);
    }

    [Fact]
    public async Task Refresh_ReturnsNewPair_ThatWorks()
    {
        var user = await _factory.CreateUserAsync();
        var jwt = await SignInAsync(user);

        var response = await RefreshAsync(jwt.RefreshToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var refreshed = await response.ReadAsync<JwtDto>();
        refreshed.RefreshToken.ShouldNotBe(jwt.RefreshToken);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
        var me = await client.GetFromJsonAsync<UserDto>("/api/users/me");
        me.ShouldNotBeNull().Id.ShouldBe(user.Id);
    }

    [Fact]
    public async Task Refresh_WithUsedToken_Returns401()
    {
        var user = await _factory.CreateUserAsync();
        var jwt = await SignInAsync(user);
        (await RefreshAsync(jwt.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var again = await RefreshAsync(jwt.RefreshToken);

        await again.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "InvalidRefreshToken");
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_Returns401()
    {
        var response = await RefreshAsync("to-nie-jest-token");

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "InvalidRefreshToken");
    }

    [Fact]
    public async Task SignOut_RevokesRefreshToken()
    {
        var user = await _factory.CreateUserAsync();
        var jwt = await SignInAsync(user);

        var signOut = await _factory.CreateClient().PostAsJsonAsync("/api/users/sign-out",
            new { refreshToken = jwt.RefreshToken });

        signOut.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await (await RefreshAsync(jwt.RefreshToken))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "InvalidRefreshToken");
    }

    [Fact]
    public async Task SignOut_WithUnknownToken_Returns204()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/users/sign-out",
            new { refreshToken = "nieznany" });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
