using ICanHelp.Api.Tests.Infrastructure;
using ICanHelp.Application.DTO;

namespace ICanHelp.Api.Tests;

/// <summary>Krok 11: wątek negocjacji (pytania przed ofertą, oś czasu, kto może pisać) i prywatność ofert.</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ConversationTests
{
    private readonly ApiFactory _factory;

    public ConversationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task QuestionBeforeOffer_ThenOfferAndEdit_AllOnOneTimeline()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();

        // Pomocnik zakłada wątek (201), drugi raz dostaje ten sam (200).
        var start = await helper.Client.PostEmptyAsync($"/api/announcements/{announcementId}/conversations");
        start.StatusCode.ShouldBe(HttpStatusCode.Created);
        var conversationId = (await start.ReadAsync<CreatedDto>()).Id;
        start.IdFromLocation().ShouldBe(conversationId);
        var url = $"/api/conversations/{conversationId}";

        var again = await helper.Client.PostEmptyAsync($"/api/announcements/{announcementId}/conversations");
        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await again.ReadAsync<CreatedDto>()).Id.ShouldBe(conversationId);

        // Pytanie przed ofertą; właściciel widzi je jako nieprzeczytane i odpowiada.
        (await SendAsync(helper, conversationId, "Czy syfon też jest do wymiany?")).StatusCode
            .ShouldBe(HttpStatusCode.Created);

        var ownerView = await owner.Client.GetFromJsonAsync<ConversationDto>(url);
        ownerView.ShouldNotBeNull();
        ownerView.MyRole.ShouldBe("Client");
        ownerView.UnreadCount.ShouldBe(1);
        ownerView.ClientId.ShouldBe(owner.Id);
        ownerView.HelperId.ShouldBe(helper.Id);
        ownerView.CanWrite.ShouldBeTrue();

        (await SendAsync(owner, conversationId, "Tak, syfon też.")).StatusCode.ShouldBe(HttpStatusCode.Created);

        // Oferta trafia do tego samego wątku (nie powstaje drugi), potem właściciel edytuje ogłoszenie.
        var offerId = await helper.MakeOfferAsync(announcementId, 150m);
        var afterOffer = await helper.Client.PostEmptyAsync($"/api/announcements/{announcementId}/conversations");
        (await afterOffer.ReadAsync<CreatedDto>()).Id.ShouldBe(conversationId);

        var edit = await owner.Client.PatchAsJsonAsync($"/api/announcements/{announcementId}",
            new { description = "Kran cieknie od tygodnia. Syfon pod zlewem też do wymiany." });
        edit.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Oś czasu u pomocnika: pytanie, odpowiedź, oferta, zmiana ogłoszenia (ostrzeżenie).
        var helperView = await helper.Client.GetFromJsonAsync<ConversationDto>(url);
        helperView.ShouldNotBeNull();
        helperView.MyRole.ShouldBe("Helper");
        helperView.AnnouncementRevision.ShouldBe(1);
        helperView.UnreadCount.ShouldBe(1); // odpowiedź właściciela
        helperView.Timeline.Select(e => e.Type).ShouldBe(new[] { "Message", "Message", "Offer", "AnnouncementChanged" });

        var question = helperView.Timeline[0].Message.ShouldNotBeNull();
        question.SenderRole.ShouldBe("Helper");
        question.Text.ShouldBe("Czy syfon też jest do wymiany?");
        question.AnnouncementRevision.ShouldBe(0);
        helperView.Timeline[1].Message!.SenderRole.ShouldBe("Client");

        var offer = helperView.Timeline[2].Offer.ShouldNotBeNull();
        offer.Id.ShouldBe(offerId);
        offer.IsOutdated.ShouldBeTrue();

        var change = helperView.Timeline[3].Change.ShouldNotBeNull();
        change.Revision.ShouldBe(1);
        change.Fields.ShouldHaveSingleItem().Field.ShouldBe("Description");

        // Przeczytane → licznik 0.
        (await helper.Client.PostEmptyAsync($"{url}/read")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await helper.Client.GetFromJsonAsync<ConversationDto>(url))!.UnreadCount.ShouldBe(0);
    }

    [Fact]
    public async Task Offer_StartsConversationAutomatically()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();

        var offerId = await helper.MakeOfferAsync(announcementId, 120m);

        var start = await helper.Client.PostEmptyAsync($"/api/announcements/{announcementId}/conversations");
        start.StatusCode.ShouldBe(HttpStatusCode.OK);
        var conversationId = (await start.ReadAsync<CreatedDto>()).Id;

        var view = await owner.Client.GetFromJsonAsync<ConversationDto>($"/api/conversations/{conversationId}");
        view!.Timeline.ShouldHaveSingleItem().Offer!.Id.ShouldBe(offerId);
    }

    [Fact]
    public async Task Owner_CannotStartConversationOnOwnAnnouncement()
    {
        var owner = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();

        var response = await owner.Client.PostEmptyAsync($"/api/announcements/{announcementId}/conversations");

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
    }

    [Fact]
    public async Task Stranger_CannotReadOrWrite()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var stranger = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var conversationId = await StartAsync(helper, announcementId);
        var url = $"/api/conversations/{conversationId}";

        await (await stranger.Client.GetAsync(url)).ShouldBeProblemAsync(HttpStatusCode.NotFound);
        await (await SendAsync(stranger, conversationId, "Dzień dobry"))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
        await (await stranger.Client.PostEmptyAsync($"{url}/read"))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");

        (await _factory.CreateClient().GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnknownConversation_Returns404()
    {
        var user = await _factory.CreateUserAsync();
        var url = $"/api/conversations/{Guid.NewGuid()}";

        await (await user.Client.GetAsync(url)).ShouldBeProblemAsync(HttpStatusCode.NotFound);
        await (await user.Client.PostAsJsonAsync($"{url}/messages", new { text = "Dzień dobry" }))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "ConversationNotFound");
    }

    [Fact]
    public async Task EmptyMessage_Returns400()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var conversationId = await StartAsync(helper, await owner.CreateAnnouncementAsync());

        await (await SendAsync(helper, conversationId, " "))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "InvalidDescription");
    }

    [Fact]
    public async Task AfterAcceptance_OnlyAcceptedHelperCanWrite()
    {
        var owner = await _factory.CreateUserAsync();
        var helperA = await _factory.CreateUserAsync();
        var helperB = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();

        var offerA = await helperA.MakeOfferAsync(announcementId, 140m);
        await helperB.MakeOfferAsync(announcementId, 130m);
        (await owner.Client.PostEmptyAsync($"/api/announcements/{announcementId}/offers/{offerA}/accept"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Istniejące wątki da się dalej otworzyć (200), ale pisać może tylko zaakceptowany pomocnik.
        var conversationA = await StartAsync(helperA, announcementId, HttpStatusCode.OK);
        var conversationB = await StartAsync(helperB, announcementId, HttpStatusCode.OK);

        (await SendAsync(helperA, conversationA, "Będę o 16:00.")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await (await SendAsync(helperB, conversationB, "A może jednak taniej?"))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");

        var viewB = await helperB.Client.GetFromJsonAsync<ConversationDto>($"/api/conversations/{conversationB}");
        viewB!.CanWrite.ShouldBeFalse();
        viewB.AnnouncementStatus.ShouldBe("Accepted");
        viewB.Timeline.ShouldHaveSingleItem().Offer!.Status.ShouldBe("Rejected");

        // Nowy pomocnik nie założy już wątku.
        var late = await _factory.CreateUserAsync();
        await (await late.Client.PostEmptyAsync($"/api/announcements/{announcementId}/conversations"))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
    }

    // ---------- prywatność ofert ----------

    [Fact]
    public async Task Offers_VisibleOnlyToOwnerAndToEachHelperOwn()
    {
        var owner = await _factory.CreateUserAsync();
        var helperA = await _factory.CreateUserAsync();
        var helperB = await _factory.CreateUserAsync();
        var stranger = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var url = $"/api/announcements/{announcementId}";

        var offerA = await helperA.MakeOfferAsync(announcementId, 140m);
        var offerB = await helperB.MakeOfferAsync(announcementId, 130m);

        var ownerView = await owner.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url);
        ownerView!.Offers.Select(o => o.Id).ShouldBe(new[] { offerA, offerB }, ignoreOrder: true);
        ownerView.HelperCount.ShouldBe(2);

        var helperView = await helperA.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url);
        helperView!.Offers.ShouldHaveSingleItem().Id.ShouldBe(offerA);
        helperView.HelperCount.ShouldBe(2);

        var strangerView = await stranger.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url);
        strangerView!.Offers.ShouldBeEmpty();
        strangerView.HelperCount.ShouldBe(2);

        var anonymousView = await _factory.CreateClient().GetFromJsonAsync<AnnouncementDetailsDto>(url);
        anonymousView!.Offers.ShouldBeEmpty();
        anonymousView.HelperCount.ShouldBe(2);
        anonymousView.Title.ShouldNotBeNullOrWhiteSpace();
    }

    // ---------- pomocnicze ----------

    private static async Task<Guid> StartAsync(TestUser helper, Guid announcementId,
        HttpStatusCode expected = HttpStatusCode.Created)
    {
        var response = await helper.Client.PostEmptyAsync($"/api/announcements/{announcementId}/conversations");
        response.StatusCode.ShouldBe(expected);
        return (await response.ReadAsync<CreatedDto>()).Id;
    }

    private static Task<HttpResponseMessage> SendAsync(TestUser user, Guid conversationId, string text)
        => user.Client.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new { text });
}
