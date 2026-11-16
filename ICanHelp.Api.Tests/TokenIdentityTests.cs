using System.Net.Http.Headers;
using System.Text;
using ICanHelp.Api.Tests.Infrastructure;
using ICanHelp.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ICanHelp.Api.Tests;

/// <summary>
/// Token musi mówić, kim jest użytkownik (claim <c>sub</c> z Guidem). Sprawdza to uwierzytelnianie – raz, dla wszystkich
/// akcji z <c>[Authorize]</c>; kontrolery czytają id (<c>CurrentUserId</c>) już bez własnych zabezpieczeń.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public sealed class TokenIdentityTests
{
    private readonly ApiFactory _factory;

    public TokenIdentityTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task SignedToken_WithoutValidUserId_IsRejected(string? subject)
    {
        var client = ClientWithToken(subject);

        (await client.GetAsync("/api/me/alerts")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/users/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.PostAsync($"/api/announcements/{Guid.NewGuid()}/cancel", content: null))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignedToken_WithUserId_IsAccepted()
    {
        // Ten sam ręcznie zbudowany token, tylko z poprawnym sub – dowód, że wyżej odrzuca go brak id, a nie podpis.
        var response = await ClientWithToken(Guid.NewGuid().ToString()).GetAsync("/api/me/alerts");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SignedToken_WithoutUserId_OnPublicEndpoint_IsTreatedAsAnonymous()
    {
        var owner = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();

        var response = await ClientWithToken(subject: null).GetAsync($"/api/announcements/{announcementId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Poprawnie podpisany token tego API (ten sam klucz, wystawca i odbiorca) – różni się tylko claimem <c>sub</c>.
    /// </summary>
    private HttpClient ClientWithToken(string? subject)
    {
        var auth = _factory.Services.GetRequiredService<IOptions<AuthOptions>>().Value;
        var claims = new Dictionary<string, object>
        {
            [AuthClaims.UserName] = "tester",
            [AuthClaims.Role] = "User"
        };

        if (subject is not null)
        {
            claims[AuthClaims.UserId] = subject;
        }

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = auth.Issuer,
            Audience = auth.Audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SigningKey)), SecurityAlgorithms.HmacSha256),
            Claims = claims
        });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
