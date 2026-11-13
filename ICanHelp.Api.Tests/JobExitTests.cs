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
    public async Task Interrupt_ByOwner_EndsJob_WithoutRatings_HelperResponds_ShownOnProfiles()
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

        // F11 G: bez ocen po przerwaniu (nie ma go w „do wystawienia”) – zamiast tego stanowisko drugiej strony.
        var pending = await helper.Client.GetFromJsonAsync<List<PendingRatingDto>>("/api/me/ratings/pending");
        pending!.ShouldNotContain(p => p.AnnouncementId == announcementId);
        await (await owner.Client.PostAsJsonAsync($"{url}/ratings", new { rate = 5 }))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
        await (await helper.Client.PostAsJsonAsync($"{url}/ratings", new { rate = 5 }))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");

        // Stanowisko: tylko pomocnik (to zleceniodawca przerwał), raz; zleceniodawca dostaje powiadomienie.
        var response = $"{url}/job-exits/1/response";
        await (await owner.Client.PostAsJsonAsync(response, new { text = "Moja wersja." }))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
        await (await helper.Client.PostAsJsonAsync(response, new { text = "x" }))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest);
        (await helper.Client.PostAsJsonAsync(response, new { text = "Zakres był inny niż w ogłoszeniu." }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await (await helper.Client.PostAsJsonAsync(response, new { text = "Drugi raz." }))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");

        var exit = (await owner.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url))!.JobExits.Single();
        exit.Response.ShouldBe("Zakres był inny niż w ogłoszeniu.");
        exit.RespondedAt.ShouldNotBeNull();

        await _factory.ProcessOutboxAsync();
        var responded = (await owner.GetNotificationsAsync()).Single(n => n.Type == "JobExitResponded");
        responded.Title.ShouldBe("Pomocnik dodał swoje stanowisko");

        // Profil publiczny (także dla niezalogowanego): przerwanie u obu stron, z rolą i stanowiskiem.
        var anonymous = _factory.CreateClient();
        var ownerProfile = await anonymous.GetFromJsonAsync<UserProfileDto>($"/api/users/{owner.Id}/profile");
        ownerProfile!.User.Id.ShouldBe(owner.Id);
        ownerProfile.AsClient.InterruptedByUser.ShouldBe(1);
        ownerProfile.AsClient.InterruptedByOther.ShouldBe(0);
        ownerProfile.AsClient.CompletedCount.ShouldBe(0);
        ownerProfile.AsHelper.InterruptedByUser.ShouldBe(0);
        var onOwner = ownerProfile.Interruptions.ShouldHaveSingleItem();
        onOwner.Role.ShouldBe("Client");
        onOwner.ByUser.ShouldBeTrue();
        onOwner.Exit.Reason.ShouldBe("Disagreement");
        onOwner.Exit.Response.ShouldBe("Zakres był inny niż w ogłoszeniu.");

        var helperProfile = await anonymous.GetFromJsonAsync<UserProfileDto>($"/api/users/{helper.Id}/profile");
        helperProfile!.AsHelper.InterruptedByOther.ShouldBe(1);
        helperProfile.AsHelper.InterruptedByUser.ShouldBe(0);
        var onHelper = helperProfile.Interruptions.ShouldHaveSingleItem();
        onHelper.Role.ShouldBe("Helper");
        onHelper.ByUser.ShouldBeFalse();

        var ownerCounts = await helper.Client.GetFromJsonAsync<UserReliabilityDto>($"/api/users/{owner.Id}/reliability");
        ownerCounts!.InterruptedCount.ShouldBe(1);
    }

    [Fact]
    public async Task Profile_CountsCompletedJobsAndRatingsPerRole()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var (announcementId, _) = await AcceptedAsync(owner, helper);
        var url = $"/api/announcements/{announcementId}";
        (await owner.Client.PostEmptyAsync($"{url}/complete")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await owner.Client.PostAsJsonAsync($"{url}/ratings", new { rate = 4 })).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await helper.Client.PostAsJsonAsync($"{url}/ratings", new { rate = 5 })).StatusCode.ShouldBe(HttpStatusCode.Created);

        var helperProfile = await helper.Client.GetFromJsonAsync<UserProfileDto>($"/api/users/{helper.Id}/profile");
        helperProfile!.AsHelper.CompletedCount.ShouldBe(1);
        helperProfile.AsHelper.RatingCount.ShouldBe(1);
        helperProfile.AsHelper.AverageRating.ShouldBe(4);
        helperProfile.AsClient.CompletedCount.ShouldBe(0);
        helperProfile.AsClient.AverageRating.ShouldBeNull();

        var ownerProfile = await helper.Client.GetFromJsonAsync<UserProfileDto>($"/api/users/{owner.Id}/profile");
        ownerProfile!.AsClient.CompletedCount.ShouldBe(1);
        ownerProfile.AsClient.AverageRating.ShouldBe(5);
        ownerProfile.Interruptions.ShouldBeEmpty();

        (await helper.Client.GetAsync($"/api/users/{Guid.NewGuid()}/profile")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
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
