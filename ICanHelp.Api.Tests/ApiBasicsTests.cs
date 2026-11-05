using ICanHelp.Api.Tests.Infrastructure;
using ICanHelp.Application.DTO;

namespace ICanHelp.Api.Tests;

[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ApiBasicsTests
{
    private readonly HttpClient _client;

    public ApiBasicsTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Categories_AreSeededAtStartup()
    {
        var categories = await _client.GetFromJsonAsync<List<CategoryDto>>("/api/categories");

        categories.ShouldNotBeNull();
        categories.Count.ShouldBeGreaterThanOrEqualTo(5);
        var plumbing = categories.Single(c => c.Name == "Plumbing");
        plumbing.Subcategories.Select(s => s.Name).ShouldContain("Leaking tap");
    }

    [Fact]
    public async Task CategoryById_ReturnsCategory()
    {
        var (categoryId, _) = await _client.GetAnySubcategoryAsync();

        var category = await _client.GetFromJsonAsync<CategoryDto>($"/api/categories/{categoryId}");

        category.ShouldNotBeNull();
        category.Id.ShouldBe(categoryId);
    }

    [Fact]
    public async Task UnknownAnnouncement_Returns404AsProblemDetails()
    {
        var response = await _client.GetAsync($"/api/announcements/{Guid.NewGuid()}");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EmptyGuid_Returns400AsProblemDetails()
    {
        var response = await _client.GetAsync($"/api/announcements/{Guid.Empty}");

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAnnouncement_WithoutToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/announcements", new { title = "Bez tokenu" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
