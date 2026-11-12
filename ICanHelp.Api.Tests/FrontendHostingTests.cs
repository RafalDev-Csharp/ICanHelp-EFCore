using ICanHelp.Api.Tests.Infrastructure;

namespace ICanHelp.Api.Tests;

/// <summary>Krok F10: API serwuje zbudowany frontend (jedna domena) – pliki, fallback na index.html, adresy zarezerwowane.</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public sealed class FrontendHostingTests
{
    private readonly HttpClient _client;

    public FrontendHostingTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/announcements")]
    [InlineData("/announcements/3f8c2a52-6c3e-4d5f-9a51-1d2b3c4d5e6f")]
    [InlineData("/profile/alerts/new")]
    public async Task AppRoutes_ReturnIndexHtml_NotCached(string path)
    {
        var response = await _client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        response.Headers.CacheControl!.NoCache.ShouldBeTrue();
        (await response.Content.ReadAsStringAsync()).ShouldContain(ApiFactory.FrontendIndexMarker);
    }

    [Fact]
    public async Task ServiceWorker_IsServedFresh()
    {
        var response = await _client.GetAsync("/app-sw.js");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/javascript");
        response.Headers.CacheControl!.NoCache.ShouldBeTrue();
    }

    [Fact]
    public async Task Manifest_HasManifestContentType()
    {
        var response = await _client.GetAsync("/manifest.webmanifest");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/manifest+json");
    }

    [Fact]
    public async Task BuildFiles_AreCacheable()
    {
        var response = await _client.GetAsync("/main-ABC123.js");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.MaxAge.ShouldBe(TimeSpan.FromDays(1));
    }

    [Fact]
    public async Task MissingFile_Returns404()
    {
        var response = await _client.GetAsync("/missing-chunk.js");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/api/unknown")]
    [InlineData("/api/announcements/unknown/path")]
    [InlineData("/hubs/unknown")]
    public async Task UnknownApiPath_Returns404AsProblemDetails_NotIndexHtml(string path)
    {
        var response = await _client.GetAsync(path);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound);
    }
}
