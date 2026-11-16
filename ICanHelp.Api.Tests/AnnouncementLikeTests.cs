using ICanHelp.Api.Tests.Infrastructure;
using ICanHelp.Application.DTO;

namespace ICanHelp.Api.Tests;

/// <summary>Krok F4 (zadania backendowe z F3): polubienia ogłoszeń i liczba pomocników na liście.</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AnnouncementLikeTests
{
    private readonly ApiFactory _factory;

    public AnnouncementLikeTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LikeAndUnlike_AreIdempotent_AndVisibleOnlyToTheLiker()
    {
        var owner = await _factory.CreateUserAsync();
        var fan = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var url = $"/api/announcements/{announcementId}";

        (await fan.Client.PutAsync($"{url}/like", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await fan.Client.PutAsync($"{url}/like", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await fan.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url))!.IsLiked.ShouldBeTrue();
        (await owner.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url))!.IsLiked.ShouldBeFalse();
        (await _factory.CreateClient().GetFromJsonAsync<AnnouncementDetailsDto>(url))!.IsLiked.ShouldBeFalse();

        var liked = await fan.Client.GetFromJsonAsync<List<LikedAnnouncementDto>>(
            $"/api/users/{fan.Id}/liked-announcements");
        liked.ShouldNotBeNull();
        liked.ShouldHaveSingleItem().AnnouncementId.ShouldBe(announcementId);

        (await fan.Client.DeleteAsync($"{url}/like")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await fan.Client.DeleteAsync($"{url}/like")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await fan.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url))!.IsLiked.ShouldBeFalse();
        (await fan.Client.GetFromJsonAsync<List<LikedAnnouncementDto>>($"/api/users/{fan.Id}/liked-announcements"))
            .ShouldNotBeNull().ShouldBeEmpty();
    }

    [Fact]
    public async Task Like_UnknownAnnouncement_Returns404_AndAnonymous401()
    {
        var fan = await _factory.CreateUserAsync();

        await (await fan.Client.PutAsync($"/api/announcements/{Guid.NewGuid()}/like", null))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "AnnouncementNotFound");

        var owner = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        (await _factory.CreateClient().PutAsync($"/api/announcements/{announcementId}/like", null)).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_ShowsHelperCount_CountingEachHelperOnce()
    {
        var owner = await _factory.CreateUserAsync();
        var firstHelper = await _factory.CreateUserAsync();
        var secondHelper = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var emptyId = await owner.CreateAnnouncementAsync(title: "Malowanie ściany");

        var offerId = await firstHelper.MakeOfferAsync(announcementId, 120m);
        await secondHelper.MakeOfferAsync(announcementId, 140m);
        // Kontroferta właściciela to nowa oferta tego samego pomocnika – liczba pomocników się nie zmienia.
        var counter = await owner.Client.PostAsJsonAsync(
            $"/api/announcements/{announcementId}/offers/{offerId}/counter", TestApi.OfferBody(130m));
        counter.StatusCode.ShouldBe(HttpStatusCode.Created);

        var list = await _factory.CreateClient().GetFromJsonAsync<List<AnnouncementDto>>(
            $"/api/announcements?ownerId={owner.Id}");
        list.ShouldNotBeNull();
        list.Single(a => a.Id == announcementId).HelperCount.ShouldBe(2);
        list.Single(a => a.Id == emptyId).HelperCount.ShouldBe(0);

        // To samo przy wyszukiwaniu po odległości (osobna ścieżka zapytania).
        var near = await _factory.CreateClient().GetFromJsonAsync<List<AnnouncementDto>>(
            $"/api/announcements?ownerId={owner.Id}&latitude=50.0614&longitude=19.9366&radiusKm=1");
        near!.Single(a => a.Id == announcementId).HelperCount.ShouldBe(2);
    }
}
