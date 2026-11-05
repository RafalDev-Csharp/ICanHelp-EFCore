using ICanHelp.Api.Tests.Infrastructure;
using ICanHelp.Application.DTO;

namespace ICanHelp.Api.Tests;

/// <summary>
/// Krok 7 na prawdziwym SQL Serverze: kolumna geography + indeks przestrzenny z migracji AnnouncementGeoPoint.
/// Każdy test filtruje po swoim właścicielu (<c>ownerId</c>), bo baza jest wspólna dla wszystkich testów.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public sealed class DistanceSearchTests
{
    // Kraków, Rynek Główny – punkt wyszukiwania.
    private const double SearchLat = 50.0617;
    private const double SearchLon = 19.9373;

    private readonly ApiFactory _factory;

    public DistanceSearchTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WithinRadius_ReturnsOnlyNearby_NearestFirst_WithDistance()
    {
        var owner = await _factory.CreateUserAsync();
        var wawel = await owner.CreateAnnouncementAsync(50.0540, 19.9354, "Wawel ~0,9 km");
        var kazimierz = await owner.CreateAnnouncementAsync(50.0510, 19.9450, "Kazimierz ~1,3 km");
        var warsaw = await owner.CreateAnnouncementAsync(52.2297, 21.0122, "Warszawa ~250 km");

        var result = await owner.Client.GetFromJsonAsync<List<AnnouncementDto>>(
            FormattableString.Invariant(
                $"/api/announcements?ownerId={owner.Id}&latitude={SearchLat}&longitude={SearchLon}&radiusKm=5"));

        result.ShouldNotBeNull();
        result.Select(a => a.Id).ShouldBe(new[] { wawel, kazimierz }); // kolejność: od najbliższego
        result.ShouldNotContain(a => a.Id == warsaw);
        result[0].DistanceKm.ShouldNotBeNull();
        result[0].DistanceKm!.Value.ShouldBeInRange(0.5, 1.5);
        result[1].DistanceKm!.Value.ShouldBeInRange(1.0, 2.0);
    }

    [Fact]
    public async Task WithoutLocation_ReturnsAll_WithoutDistance()
    {
        var owner = await _factory.CreateUserAsync();
        await owner.CreateAnnouncementAsync(50.0540, 19.9354);
        await owner.CreateAnnouncementAsync(52.2297, 21.0122);

        var result = await owner.Client.GetFromJsonAsync<List<AnnouncementDto>>(
            $"/api/announcements?ownerId={owner.Id}");

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldAllBe(a => a.DistanceKm == null);
    }

    [Theory]
    [InlineData("latitude=50.06")]                                  // brak długości
    [InlineData("latitude=95&longitude=19.9")]                      // poza zakresem
    [InlineData("latitude=50.06&longitude=19.9&radiusKm=500")]      // za duży promień
    [InlineData("radiusKm=5")]                                      // promień bez punktu
    public async Task InvalidDistanceFilter_Returns400(string query)
    {
        var response = await _factory.CreateClient().GetAsync($"/api/announcements?{query}");

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "InvalidDistanceFilter");
    }
}
