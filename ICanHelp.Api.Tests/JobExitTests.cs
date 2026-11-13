using ICanHelp.Api.Tests.Infrastructure;
using ICanHelp.Application.DTO;

namespace ICanHelp.Api.Tests;

/// <summary>F11 E: rezygnacja przed rozpoczęciem, przerwanie w trakcie, powiadomienia, oceny i liczniki przez HTTP.</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public sealed class JobExitTests
{
    private readonly ApiFactory _factory;

    public JobExitTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Abandon_ByHelper_ReopensAnnouncement_ClosesHisThread_AndCounts()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var otherHelper = await _factory.CreateUserAsync();
        var stranger = await _factory.CreateUserAsync();
        var (announcementId, offerId) = await AcceptedAsync(owner, helper);
        var url = $"/api/announcements/{announcementId}";

        // Obcy nie może; powód „Inny” bez opisu → 400; Disagreement przed startem → 400.
        await (await stranger.Client.PostAsJsonAsync($"{url}/abandon", new { reason = "NoContact" }))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
        await (await helper.Client.PostAsJsonAsync($"{url}/abandon", new { reason = "Other" }))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "InvalidJobExit");
        await (await helper.Client.PostAsJsonAsync($"{url}/abandon", new { reason = "Disagreement" }))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "InvalidJobExit");
        // Przerwać można dopiero w trakcie.
        await (await helper.Client.PostAsJsonAsync($"{url}/interrupt", new { reason = "NoContact" }))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");

        (await helper.Client.PostAsJsonAsync($"{url}/abandon", new { reason = "CannotMakeIt", note = "Choroba." }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Ogłoszenie znów otwarte, oferta „zerwana”, wpis w historii (właściciel i ten pomocnik widzą, obcy – nie).
        var details = await owner.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url);
        details!.Status.ShouldBe("Open");
        details.AcceptedOfferId.ShouldBeNull();
        details.Offers.Single(o => o.Id == offerId).Status.ShouldBe("Abandoned");
        var exit = details.JobExits.ShouldHaveSingleItem();
        exit.Kind.ShouldBe("Abandoned");
        exit.ActorId.ShouldBe(helper.Id);
        exit.ActorRole.ShouldBe("Helper");
        exit.HelperId.ShouldBe(helper.Id);
        exit.OfferId.ShouldBe(offerId);
        exit.Reason.ShouldBe("CannotMakeIt");
        exit.Note.ShouldBe("Choroba.");
        (await helper.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url))!.JobExits.Count.ShouldBe(1);
        (await stranger.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url))!.JobExits.ShouldBeEmpty();

        // Wątek pomocnika: tylko do odczytu, z wpisem w osi czasu. Nowej oferty nie złoży; inny pomocnik – tak.
        var conversationId = (await (await helper.Client.PostEmptyAsync($"{url}/conversations"))
            .ReadAsync<CreatedDto>()).Id;
        var thread = await helper.Client.GetFromJsonAsync<ConversationDto>($"/api/conversations/{conversationId}");
        thread!.CanWrite.ShouldBeFalse();
        thread.Timeline.Single(e => e.Type == "JobExit").JobExit!.Reason.ShouldBe("CannotMakeIt");

        await (await helper.Client.PostAsJsonAsync($"{url}/offers", TestApi.OfferBody(110m)))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
        await otherHelper.MakeOfferAsync(announcementId, 130m);

        // Powiadomienie u właściciela z powodem; licznik liczy tylko tego, kto zrezygnował.
        await _factory.ProcessOutboxAsync();
        var notification = (await owner.GetNotificationsAsync()).Single(n => n.Type == "JobAbandoned");
        notification.ConversationId.ShouldBe(conversationId);
        notification.Title.ShouldBe("Pomocnik zrezygnował ze zlecenia");
        notification.Body.ShouldContain("nie mogę w tym terminie (Choroba.)");

        var helperCounts = await owner.Client.GetFromJsonAsync<UserReliabilityDto>($"/api/users/{helper.Id}/reliability");
        helperCounts!.AbandonedCount.ShouldBe(1);
        helperCounts.InterruptedCount.ShouldBe(0);
        var ownerCounts = await helper.Client.GetFromJsonAsync<UserReliabilityDto>($"/api/users/{owner.Id}/reliability");
        ownerCounts!.AbandonedCount.ShouldBe(0);

        // Bez ocen po rezygnacji.
        await (await owner.Client.PostAsJsonAsync($"{url}/ratings", new { rate = 1 }))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
    }

    [Fact]
    public async Task Interrupt_ByOwner_EndsJob_NotifiesHelper_AndBothCanRate()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var (announcementId, _) = await AcceptedAsync(owner, helper);
        var url = $"/api/announcements/{announcementId}";
        (await helper.Client.PostEmptyAsync($"{url}/start")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Po starcie rezygnacji już nie ma – jest przerwanie.
        await (await owner.Client.PostAsJsonAsync($"{url}/abandon", new { reason = "NoContact" }))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");

        (await owner.Client.PostAsJsonAsync($"{url}/interrupt", new { reason = "Disagreement" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var details = await helper.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url);
        details!.Status.ShouldBe("Interrupted");
        details.JobExits.ShouldHaveSingleItem().ActorRole.ShouldBe("Client");

        // Kontakt dalej widać (rozliczenie za wykonaną część), wątek tylko do odczytu.
        (await helper.Client.GetAsync($"{url}/contact")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await (await owner.Client.PostEmptyAsync($"{url}/complete"))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");

        await _factory.ProcessOutboxAsync();
        var notification = (await helper.GetNotificationsAsync()).Single(n => n.Type == "JobInterrupted");
        notification.Title.ShouldBe("Zleceniodawca przerwał zlecenie");

        // Do wystawienia – z informacją o przerwaniu; obie strony oceniają.
        var pending = await helper.Client.GetFromJsonAsync<List<PendingRatingDto>>("/api/me/ratings/pending");
        var item = pending!.Single(p => p.AnnouncementId == announcementId);
        item.Interruption!.Reason.ShouldBe("Disagreement");

        (await owner.Client.PostAsJsonAsync($"{url}/ratings", new { rate = 2, comment = "Nie dokończył." }))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        (await helper.Client.PostAsJsonAsync($"{url}/ratings", new { rate = 3 }))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var received = await helper.Client.GetFromJsonAsync<List<MyRatingDto>>("/api/me/ratings");
        received!.Single(r => r.AnnouncementId == announcementId).Interruption!.ActorRole.ShouldBe("Client");

        var ownerCounts = await helper.Client.GetFromJsonAsync<UserReliabilityDto>($"/api/users/{owner.Id}/reliability");
        ownerCounts!.InterruptedCount.ShouldBe(1);
    }

    [Fact]
    public async Task Reliability_UnknownUser_Is404()
        => (await _factory.CreateClient().GetAsync($"/api/users/{Guid.NewGuid()}/reliability"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

    private static async Task<(Guid AnnouncementId, Guid OfferId)> AcceptedAsync(TestUser owner, TestUser helper)
    {
        var announcementId = await owner.CreateAnnouncementAsync();
        var offerId = await helper.MakeOfferAsync(announcementId, 120m);
        (await owner.Client.PostEmptyAsync($"/api/announcements/{announcementId}/offers/{offerId}/accept"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        return (announcementId, offerId);
    }
}
