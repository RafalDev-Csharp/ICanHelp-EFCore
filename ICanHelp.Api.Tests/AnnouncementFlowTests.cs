using ICanHelp.Api.Tests.Infrastructure;
using ICanHelp.Application.DTO;

namespace ICanHelp.Api.Tests;

/// <summary>Cały przepływ z CLAUDE.md przez HTTP: ogłoszenie → oferta → kontroferta → akceptacja → start → koniec → oceny.</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AnnouncementFlowTests
{
    private readonly ApiFactory _factory;

    public AnnouncementFlowTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FullFlow_FromAnnouncementToRatings()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var stranger = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var url = $"/api/announcements/{announcementId}";

        // Oferta pomocnika; przed akceptacją kontaktu nie ma.
        var offerId = await helper.MakeOfferAsync(announcementId, 120m);
        (await helper.Client.GetAsync($"{url}/contact")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Kontroferta właściciela, pomocnik ją akceptuje.
        var counter = await owner.Client.PostAsJsonAsync($"{url}/offers/{offerId}/counter", TestApi.OfferBody(100m));
        counter.StatusCode.ShouldBe(HttpStatusCode.Created);
        var counterId = (await counter.ReadAsync<CreatedDto>()).Id;

        (await helper.Client.PostEmptyAsync($"{url}/offers/{counterId}/accept")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        var details = await owner.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url);
        details.ShouldNotBeNull();
        details.Status.ShouldBe("Accepted");
        details.AcceptedOfferId.ShouldBe(counterId);
        details.Offers.Single(o => o.Id == offerId).Status.ShouldBe("Countered");
        var accepted = details.Offers.Single(o => o.Id == counterId);
        accepted.Status.ShouldBe("Accepted");
        accepted.ParentOfferId.ShouldBe(offerId);
        accepted.ProposedBy.ShouldBe("Client");
        accepted.Price.Amount.ShouldBe(100m);

        // Po akceptacji strony widzą nawzajem swój kontakt, obcy – nie.
        var ownerContact = await helper.Client.GetFromJsonAsync<AnnouncementContactDto>($"{url}/contact");
        ownerContact!.UserId.ShouldBe(owner.Id);
        ownerContact.ContactData.Email.ShouldBe(owner.Email);
        var helperContact = await owner.Client.GetFromJsonAsync<AnnouncementContactDto>($"{url}/contact");
        helperContact!.UserId.ShouldBe(helper.Id);
        (await stranger.Client.GetAsync($"{url}/contact")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Start (tylko pomocnik), zgłoszenie wykonania (pomocnik, raz) i potwierdzenie (tylko właściciel).
        await (await owner.Client.PostEmptyAsync($"{url}/start"))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
        (await helper.Client.PostEmptyAsync($"{url}/start")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await (await owner.Client.PostEmptyAsync($"{url}/report-done"))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
        (await helper.Client.PostEmptyAsync($"{url}/report-done")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await (await helper.Client.PostEmptyAsync($"{url}/report-done"))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");

        var reported = await owner.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url);
        reported!.Status.ShouldBe("InProgress");
        reported.DoneReportedAt.ShouldNotBeNull();

        await (await helper.Client.PostEmptyAsync($"{url}/complete"))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
        (await owner.Client.PostEmptyAsync($"{url}/complete")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Oceny: każda strona raz; drugi raz → 409.
        var ownerRates = await owner.Client.PostAsJsonAsync($"{url}/ratings", new { rate = 5, comment = "Polecam." });
        ownerRates.StatusCode.ShouldBe(HttpStatusCode.Created);
        var again = await owner.Client.PostAsJsonAsync($"{url}/ratings", new { rate = 1 });
        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "AlreadyRated");
        (await helper.Client.PostAsJsonAsync($"{url}/ratings", new { rate = 4 })).StatusCode
            .ShouldBe(HttpStatusCode.Created);

        var ratings = await owner.Client.GetFromJsonAsync<List<RatingDto>>($"{url}/ratings");
        ratings.ShouldNotBeNull();
        ratings.Count.ShouldBe(2);
        ratings.Single(r => r.RaterId == owner.Id).RateeId.ShouldBe(helper.Id);
        ratings.Single(r => r.RaterId == helper.Id).RateeId.ShouldBe(owner.Id);

        var helperRatings = await _factory.CreateClient()
            .GetFromJsonAsync<List<RatingDto>>($"/api/users/{helper.Id}/ratings");
        helperRatings.ShouldNotBeNull();
        helperRatings.ShouldHaveSingleItem().Rate.ShouldBe(5);
    }

    [Fact]
    public async Task Job_NotifiesOtherSide_AndOwnerCanCompleteWithoutStart()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();

        // 1) Pełna realizacja: start → zgłoszenie → potwierdzenie, każda strona dostaje swoje powiadomienia.
        var announcementId = await owner.CreateAnnouncementAsync();
        var url = $"/api/announcements/{announcementId}";
        var offerId = await helper.MakeOfferAsync(announcementId, 120m);
        (await owner.Client.PostEmptyAsync($"{url}/offers/{offerId}/accept")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);
        await _factory.ProcessOutboxAsync();
        var ownerBefore = (await owner.GetNotificationsAsync()).Count;
        var helperBefore = (await helper.GetNotificationsAsync()).Count;

        (await helper.Client.PostEmptyAsync($"{url}/start")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await helper.Client.PostEmptyAsync($"{url}/report-done")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await owner.Client.PostEmptyAsync($"{url}/complete")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _factory.ProcessOutboxAsync();

        var ownerNotifications = (await owner.GetNotificationsAsync())
            .Where(n => n.AnnouncementId == announcementId).ToList();
        (await owner.GetNotificationsAsync()).Count.ShouldBe(ownerBefore + 2);
        ownerNotifications.ShouldContain(n => n.Type == "JobStarted" && n.Title == "Pomocnik rozpoczął zlecenie");
        var done = ownerNotifications.Single(n => n.Type == "JobDoneReported");
        done.ConversationId.ShouldNotBeNull();
        done.Url.ShouldBe($"/conversations/{done.ConversationId}");

        var helperNotifications = await helper.GetNotificationsAsync();
        helperNotifications.Count.ShouldBe(helperBefore + 1);
        helperNotifications.Single(n => n.Type == "JobCompleted").Title.ShouldBe("Zlecenie zakończone – możesz ocenić");

        var conversation = await helper.Client.GetFromJsonAsync<ConversationDto>($"/api/conversations/{done.ConversationId}");
        conversation!.AnnouncementStatus.ShouldBe("Completed");
        conversation.AnnouncementDoneReportedAt.ShouldNotBeNull();
        conversation.CanWrite.ShouldBeFalse();

        // 2) Pomocnik zapomniał „Rozpocznij” – zleceniodawca i tak potwierdza wykonanie z Accepted.
        var secondId = await owner.CreateAnnouncementAsync();
        var secondUrl = $"/api/announcements/{secondId}";
        var secondOffer = await helper.MakeOfferAsync(secondId, 90m);
        (await owner.Client.PostEmptyAsync($"{secondUrl}/offers/{secondOffer}/accept")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);
        (await owner.Client.PostEmptyAsync($"{secondUrl}/complete")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await owner.Client.GetFromJsonAsync<AnnouncementDetailsDto>(secondUrl))!.Status.ShouldBe("Completed");
    }

    [Fact]
    public async Task AcceptingOffer_RejectsOtherPendingOffers()
    {
        var owner = await _factory.CreateUserAsync();
        var helperA = await _factory.CreateUserAsync();
        var helperB = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var url = $"/api/announcements/{announcementId}";

        var offerA = await helperA.MakeOfferAsync(announcementId, 140m);
        var offerB = await helperB.MakeOfferAsync(announcementId, 130m);

        (await owner.Client.PostEmptyAsync($"{url}/offers/{offerB}/accept")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        var details = await owner.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url);
        details!.Offers.Single(o => o.Id == offerA).Status.ShouldBe("Rejected");
        details.Offers.Single(o => o.Id == offerB).Status.ShouldBe("Accepted");

        // Po akceptacji ogłoszenie nie przyjmuje nowych ofert.
        var late = await _factory.CreateUserAsync();
        var lateOffer = await late.Client.PostAsJsonAsync($"{url}/offers", TestApi.OfferBody(90m));
        await lateOffer.ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
    }

    [Fact]
    public async Task Owner_CannotMakeOfferOnOwnAnnouncement()
    {
        var owner = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();

        var response = await owner.Client.PostAsJsonAsync(
            $"/api/announcements/{announcementId}/offers", TestApi.OfferBody(100m));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
    }

    [Fact]
    public async Task Helper_CannotAcceptOwnOffer_ButCanWithdrawIt()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var offerId = await helper.MakeOfferAsync(announcementId, 120m);
        var offerUrl = $"/api/announcements/{announcementId}/offers/{offerId}";

        await (await helper.Client.PostEmptyAsync($"{offerUrl}/accept"))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
        (await helper.Client.PostEmptyAsync($"{offerUrl}/withdraw")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var details = await owner.Client.GetFromJsonAsync<AnnouncementDetailsDto>($"/api/announcements/{announcementId}");
        details!.Offers.ShouldHaveSingleItem().Status.ShouldBe("Withdrawn");
    }

    [Fact]
    public async Task UnknownOffer_Returns404()
    {
        var owner = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();

        var response = await owner.Client.PostEmptyAsync(
            $"/api/announcements/{announcementId}/offers/{Guid.NewGuid()}/accept");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "OfferNotFound");
    }

    [Fact]
    public async Task Cancel_OnlyByOwner()
    {
        var owner = await _factory.CreateUserAsync();
        var other = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var url = $"/api/announcements/{announcementId}";

        await (await other.Client.PostEmptyAsync($"{url}/cancel"))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
        (await owner.Client.PostEmptyAsync($"{url}/cancel")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var details = await owner.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url);
        details!.Status.ShouldBe("Cancelled");
    }
}
