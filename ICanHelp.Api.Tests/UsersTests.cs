using ICanHelp.Api.Tests.Infrastructure;
using ICanHelp.Application.DTO;

namespace ICanHelp.Api.Tests;

[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public sealed class UsersTests
{
    private readonly ApiFactory _factory;

    public UsersTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SignUp_SignIn_Me_ReturnsOwnFullProfile()
    {
        var user = await _factory.CreateUserAsync();

        var me = await user.Client.GetFromJsonAsync<UserDto>("/api/users/me");

        me.ShouldNotBeNull();
        me.Id.ShouldBe(user.Id);
        me.UserName.ShouldBe(user.UserName);
        me.Role.ShouldBe("User");
        me.IsLocked.ShouldBeFalse();
        me.ContactData.Email.ShouldBe(user.Email);
    }

    [Fact]
    public async Task SignUp_WithTakenEmail_Returns409()
    {
        var existing = await _factory.CreateUserAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users/sign-up",
            TestApi.SignUpBody($"other{Guid.NewGuid():N}"[..20], existing.Email));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "EmailAlreadyInUse");
    }

    [Fact]
    public async Task SignUp_WithInvalidPhone_Returns400()
    {
        var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..10];

        var response = await client.PostAsJsonAsync("/api/users/sign-up", new
        {
            userName = $"user{suffix}",
            fullName = "Test User",
            email = $"user{suffix}@example.com",
            password = TestApi.Password,
            phoneNumber = "abc",
            country = "Polska",
            city = "Kraków",
            postalCode = "30-001"
        });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "InvalidPhoneNumber");
    }

    [Fact]
    public async Task SignIn_WithWrongPassword_Returns401()
    {
        var user = await _factory.CreateUserAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users/sign-in",
            new { email = user.Email, password = "ZleHaslo123!" });

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "InvalidCredentials");
    }

    [Fact]
    public async Task SignIn_WithUnknownEmail_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users/sign-in",
            new { email = $"nobody{Guid.NewGuid():N}@example.com", password = TestApi.Password });

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "InvalidCredentials");
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/users/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithTokenSignedByOtherKey_Returns401()
    {
        var client = _factory.CreateClient();
        // Poprawny format JWT, zły podpis.
        client.DefaultRequestHeaders.Authorization = new("Bearer",
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjMifQ.c2lnbmF0dXJlLXdyb25n");

        var response = await client.GetAsync("/api/users/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UserList_ForRegularUser_Returns403()
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.Client.GetAsync("/api/users");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PublicProfile_DoesNotExposeContactData()
    {
        var user = await _factory.CreateUserAsync();

        var response = await _factory.CreateClient().GetAsync($"/api/users/{user.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.ShouldContain(user.UserName);
        json.ShouldNotContain(user.Email);
        json.ShouldNotContain("600100200");
    }

    [Fact]
    public async Task LikedAnnouncements_OfAnotherUser_Returns403()
    {
        var user = await _factory.CreateUserAsync();
        var other = await _factory.CreateUserAsync();

        var own = await user.Client.GetAsync($"/api/users/{user.Id}/liked-announcements");
        var foreign = await user.Client.GetAsync($"/api/users/{other.Id}/liked-announcements");

        own.StatusCode.ShouldBe(HttpStatusCode.OK);
        foreign.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
