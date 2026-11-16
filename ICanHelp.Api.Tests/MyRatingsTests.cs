using ICanHelp.Api.Tests.Infrastructure;
using ICanHelp.Application.DTO;

namespace ICanHelp.Api.Tests;

/// <summary><c>api/me/ratings</c> – otrzymane oceny z rolą i „do wystawienia” (ekran „Oceny”, F11 F).</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MyRatingsTests
{
    private readonly ApiFactory _factory;

    public MyRatingsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PendingAndReceived_FollowTheCompletedJob()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var other = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var url = $"/api/announcements/{announcementId}";

        // Oferta innego pomocnika zostaje odrzucona przy akceptacji – on nic nie ocenia.
        await other.MakeOfferAsync(announcementId, 90m);
        var offerId = await helper.MakeOfferAsync(announcementId, 100m);
        (await owner.Client.PostEmptyAsync($"{url}/offers/{offerId}/accept")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        // Przed zakończeniem nie ma czego oceniać.
        (await GetPendingAsync(owner)).ShouldNotContain(p => p.AnnouncementId == announcementId);

        (await owner.Client.PostEmptyAsync($"{url}/complete")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var ownerPending = (await GetPendingAsync(owner)).ShouldHaveSingleItem();
        ownerPending.AnnouncementId.ShouldBe(announcementId);
        ownerPending.MyRole.ShouldBe("Client");
        ownerPending.OtherParty.Id.ShouldBe(helper.Id);
        ownerPending.OtherParty.UserName.ShouldBe(helper.UserName);
        ownerPending.AnnouncementTitle.ShouldNotBeNullOrWhiteSpace();

        var helperPending = (await GetPendingAsync(helper)).ShouldHaveSingleItem();
        helperPending.MyRole.ShouldBe("Helper");
        helperPending.OtherParty.Id.ShouldBe(owner.Id);
        (await GetPendingAsync(other)).ShouldBeEmpty();

        // Właściciel ocenia – znika z jego „do wystawienia”, pomocnik widzi ocenę „jako pomocnik”.
        (await owner.Client.PostAsJsonAsync($"{url}/ratings", new { rate = 5, comment = "Polecam." })).StatusCode
            .ShouldBe(HttpStatusCode.Created);
        (await GetPendingAsync(owner)).ShouldBeEmpty();
        (await GetPendingAsync(helper)).ShouldHaveSingleItem();

        var helperReceived = (await GetReceivedAsync(helper)).ShouldHaveSingleItem();
        helperReceived.AnnouncementId.ShouldBe(announcementId);
        helperReceived.MyRole.ShouldBe("Helper");
        helperReceived.Rate.ShouldBe(5);
        helperReceived.Comment.ShouldBe("Polecam.");
        helperReceived.Rater.Id.ShouldBe(owner.Id);
        helperReceived.Rater.UserName.ShouldBe(owner.UserName);
        (await GetReceivedAsync(owner)).ShouldBeEmpty();

        // Pomocnik ocenia – właściciel ma ocenę „jako zleceniodawca”.
        (await helper.Client.PostAsJsonAsync($"{url}/ratings", new { rate = 4 })).StatusCode
            .ShouldBe(HttpStatusCode.Created);
        (await GetPendingAsync(helper)).ShouldBeEmpty();
        var ownerReceived = (await GetReceivedAsync(owner)).ShouldHaveSingleItem();
        ownerReceived.MyRole.ShouldBe("Client");
        ownerReceived.Rate.ShouldBe(4);
        ownerReceived.Comment.ShouldBeNull();
    }

    [Fact]
    public async Task Anonymous_Gets401()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/me/ratings")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/me/ratings/pending")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<List<PendingRatingDto>> GetPendingAsync(TestUser user)
        => (await user.Client.GetFromJsonAsync<List<PendingRatingDto>>("/api/me/ratings/pending"))!;

    private static async Task<List<MyRatingDto>> GetReceivedAsync(TestUser user)
        => (await user.Client.GetFromJsonAsync<List<MyRatingDto>>("/api/me/ratings"))!;
}
