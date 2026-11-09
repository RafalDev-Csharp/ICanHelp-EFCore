using ICanHelp.Api.Tests.Infrastructure;
using ICanHelp.Application.DTO;

namespace ICanHelp.Api.Tests;

/// <summary>Krok 12: „moje negocjacje” – lista wątków z podglądem, licznikiem nieprzeczytanych i flagą zmiany ogłoszenia.</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MyConversationsTests
{
    private const string Url = "/api/me/conversations";

    private readonly ApiFactory _factory;

    public MyConversationsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TwoConversations_NewestActivityFirst_WithPreview()
    {
        var owner = await _factory.CreateUserAsync();
        var helperA = await _factory.CreateUserAsync();
        var helperB = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync(title: "Malowanie przedpokoju");

        await helperA.MakeOfferAsync(announcementId, 140m);
        var offerB = await helperB.MakeOfferAsync(announcementId, 130m);
        var conversationA = await StartAsync(helperA, announcementId);
        var conversationB = await StartAsync(helperB, announcementId);

        // Wiadomość A jest najnowszą aktywnością → A pierwsze.
        (await SendAsync(helperA, conversationA, "Mogę jutro po 17:00.")).StatusCode.ShouldBe(HttpStatusCode.Created);

        var list = await GetAsync(owner);
        list.Select(c => c.ConversationId).ShouldBe(new[] { conversationA, conversationB });

        var a = list[0];
        a.AnnouncementId.ShouldBe(announcementId);
        a.Title.ShouldBe("Malowanie przedpokoju");
        a.AnnouncementStatus.ShouldBe("Open");
        a.MyRole.ShouldBe("Client");
        a.OtherParty.Id.ShouldBe(helperA.Id);
        a.OtherParty.UserName.ShouldBe(helperA.UserName);
        a.LastMessagePreview.ShouldBe("Mogę jutro po 17:00.");
        a.UnreadCount.ShouldBe(1);
        a.AnnouncementChangedSinceLastRead.ShouldBeFalse();

        var b = list[1];
        b.OtherParty.Id.ShouldBe(helperB.Id);
        b.LastMessagePreview.ShouldBeNull();
        b.UnreadCount.ShouldBe(0);
        var lastOfferB = b.LastOffer.ShouldNotBeNull();
        lastOfferB.Id.ShouldBe(offerB);
        lastOfferB.Price.Amount.ShouldBe(130m);
        lastOfferB.Status.ShouldBe("Pending");
        lastOfferB.ProposedBy.ShouldBe("Helper");
        lastOfferB.IsOutdated.ShouldBeFalse();
        a.LastActivityAt.ShouldBeGreaterThan(b.LastActivityAt);

        // Kontroferta właściciela w B → B wraca na górę, ostatnia oferta = kontroferta.
        var counter = await owner.Client.PostAsJsonAsync(
            $"/api/announcements/{announcementId}/offers/{offerB}/counter", TestApi.OfferBody(135m));
        counter.StatusCode.ShouldBe(HttpStatusCode.Created);
        var counterId = (await counter.ReadAsync<CreatedDto>()).Id;

        list = await GetAsync(owner);
        list.Select(c => c.ConversationId).ShouldBe(new[] { conversationB, conversationA });
        var counterOffer = list[0].LastOffer.ShouldNotBeNull();
        counterOffer.Id.ShouldBe(counterId);
        counterOffer.ProposedBy.ShouldBe("Client");
        counterOffer.Price.Amount.ShouldBe(135m);

        // Stronicowanie.
        var secondPage = await GetAsync(owner, "?page=2&pageSize=1");
        secondPage.ShouldHaveSingleItem().ConversationId.ShouldBe(conversationA);
    }

    [Fact]
    public async Task UnreadCount_ResetsAfterRead_SeenFromBothSides()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var conversationId = await StartAsync(helper, announcementId, HttpStatusCode.Created);

        (await SendAsync(helper, conversationId, "Dzień dobry, jaki to kran?")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await SendAsync(helper, conversationId, "I czy jest zawór pod zlewem?")).StatusCode.ShouldBe(HttpStatusCode.Created);

        var ownerRow = (await GetAsync(owner)).ShouldHaveSingleItem();
        ownerRow.UnreadCount.ShouldBe(2);
        ownerRow.LastMessagePreview.ShouldBe("I czy jest zawór pod zlewem?");
        ownerRow.LastOffer.ShouldBeNull();

        // Pomocnik pisał sam – u niego nic nieprzeczytanego; druga strona to właściciel.
        var helperRow = (await GetAsync(helper)).ShouldHaveSingleItem();
        helperRow.MyRole.ShouldBe("Helper");
        helperRow.OtherParty.Id.ShouldBe(owner.Id);
        helperRow.UnreadCount.ShouldBe(0);

        (await owner.Client.PostEmptyAsync($"/api/conversations/{conversationId}/read")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);
        (await GetAsync(owner)).ShouldHaveSingleItem().UnreadCount.ShouldBe(0);

        // Odpowiedź właściciela → nieprzeczytana u pomocnika.
        (await SendAsync(owner, conversationId, "Zwykły, jednouchwytowy. Zawór jest.")).StatusCode
            .ShouldBe(HttpStatusCode.Created);
        (await GetAsync(helper)).ShouldHaveSingleItem().UnreadCount.ShouldBe(1);
    }

    [Fact]
    public async Task AnnouncementEdit_FlagsHelperUntilRead_AndOutdatesOffer()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var offerId = await helper.MakeOfferAsync(announcementId, 150m);
        var conversationId = await StartAsync(helper, announcementId);

        (await GetAsync(helper)).ShouldHaveSingleItem().AnnouncementChangedSinceLastRead.ShouldBeFalse();
        var beforeEdit = (await GetAsync(helper)).Single().LastActivityAt;

        var edit = await owner.Client.PatchAsJsonAsync($"/api/announcements/{announcementId}",
            new { budget = new { amount = 120m, currency = "PLN" } });
        edit.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var row = (await GetAsync(helper)).ShouldHaveSingleItem();
        row.AnnouncementChangedSinceLastRead.ShouldBeTrue();
        row.LastActivityAt.ShouldBeGreaterThan(beforeEdit);
        var lastOffer = row.LastOffer.ShouldNotBeNull();
        lastOffer.Id.ShouldBe(offerId);
        lastOffer.IsOutdated.ShouldBeTrue();

        // Właściciel edytował sam – u niego bez ostrzeżenia.
        (await GetAsync(owner)).ShouldHaveSingleItem().AnnouncementChangedSinceLastRead.ShouldBeFalse();

        // Pomocnik otworzył wątek i oznaczył jako przeczytany → flaga znika.
        (await helper.Client.PostEmptyAsync($"/api/conversations/{conversationId}/read")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);
        (await GetAsync(helper)).ShouldHaveSingleItem().AnnouncementChangedSinceLastRead.ShouldBeFalse();
    }

    [Fact]
    public async Task Filters_ByRoleAndAnnouncementStatus()
    {
        // „user” jest klientem we własnym ogłoszeniu i pomocnikiem w cudzym.
        var user = await _factory.CreateUserAsync();
        var someHelper = await _factory.CreateUserAsync();
        var otherOwner = await _factory.CreateUserAsync();

        var ownAnnouncement = await user.CreateAnnouncementAsync();
        await someHelper.MakeOfferAsync(ownAnnouncement, 100m);
        var asClient = await StartAsync(someHelper, ownAnnouncement);

        var foreignAnnouncement = await otherOwner.CreateAnnouncementAsync();
        var offer = await user.MakeOfferAsync(foreignAnnouncement, 90m);
        var asHelper = await StartAsync(user, foreignAnnouncement);
        (await otherOwner.Client.PostEmptyAsync($"/api/announcements/{foreignAnnouncement}/offers/{offer}/accept"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await GetAsync(user)).Select(c => c.ConversationId).ShouldBe(new[] { asClient, asHelper }, ignoreOrder: true);
        (await GetAsync(user, "?role=Client")).ShouldHaveSingleItem().ConversationId.ShouldBe(asClient);

        var helperRow = (await GetAsync(user, "?role=Helper")).ShouldHaveSingleItem();
        helperRow.ConversationId.ShouldBe(asHelper);
        helperRow.AnnouncementStatus.ShouldBe("Accepted");
        helperRow.LastOffer!.Status.ShouldBe("Accepted");

        (await GetAsync(user, "?status=Open")).ShouldHaveSingleItem().ConversationId.ShouldBe(asClient);
        (await GetAsync(user, "?status=Accepted&role=Helper")).ShouldHaveSingleItem().ConversationId.ShouldBe(asHelper);
        (await GetAsync(user, "?status=Accepted&role=Client")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Filter_ByAnnouncementId_ReturnsOnlyThatAnnouncementThreads()
    {
        var owner = await _factory.CreateUserAsync();
        var helperA = await _factory.CreateUserAsync();
        var helperB = await _factory.CreateUserAsync();
        var first = await owner.CreateAnnouncementAsync();
        var second = await owner.CreateAnnouncementAsync();

        await helperA.MakeOfferAsync(first, 100m);
        await helperB.MakeOfferAsync(first, 110m);
        await helperA.MakeOfferAsync(second, 120m);
        var firstA = await StartAsync(helperA, first);
        var firstB = await StartAsync(helperB, first);
        var secondA = await StartAsync(helperA, second);

        (await GetAsync(owner, $"?announcementId={first}")).Select(c => c.ConversationId)
            .ShouldBe(new[] { firstA, firstB }, ignoreOrder: true);
        (await GetAsync(owner, $"?announcementId={second}")).ShouldHaveSingleItem().ConversationId.ShouldBe(secondA);

        // Pomocnik widzi tylko swój wątek, obcy – nic.
        (await GetAsync(helperB, $"?announcementId={first}")).ShouldHaveSingleItem().ConversationId.ShouldBe(firstB);
        (await GetAsync(helperB, $"?announcementId={second}")).ShouldBeEmpty();
    }

    [Fact]
    public async Task WithoutConversations_EmptyList_AndAnonymous401()
    {
        var user = await _factory.CreateUserAsync();
        (await GetAsync(user)).ShouldBeEmpty();

        (await _factory.CreateClient().GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---------- pomocnicze ----------

    private static async Task<List<MyConversationDto>> GetAsync(TestUser user, string queryString = "")
    {
        var response = await user.Client.GetAsync(Url + queryString);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadAsync<List<MyConversationDto>>();
    }

    /// <summary>Id wątku (po ofercie już istnieje → 200).</summary>
    private static async Task<Guid> StartAsync(TestUser helper, Guid announcementId,
        HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await helper.Client.PostEmptyAsync($"/api/announcements/{announcementId}/conversations");
        response.StatusCode.ShouldBe(expected);
        return (await response.ReadAsync<CreatedDto>()).Id;
    }

    private static Task<HttpResponseMessage> SendAsync(TestUser user, Guid conversationId, string text)
        => user.Client.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new { text });
}
