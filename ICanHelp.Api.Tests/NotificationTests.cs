using ICanHelp.Api.Tests.Infrastructure;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Notifications;

namespace ICanHelp.Api.Tests;

/// <summary>
/// Krok 13: zdarzenie → outbox → powiadomienie dla drugiej strony. Outbox przetwarzamy ręcznie
/// (<see cref="TestApi.ProcessOutboxAsync"/>), bo w testach pętla w tle jest wyłączona.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public sealed class NotificationTests
{
    private readonly ApiFactory _factory;

    public NotificationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Offer_NotifiesOwner_CounterOffer_NotifiesHelper_NeverTheAuthor()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync(title: "Wymiana baterii w łazience");

        var offerId = await helper.MakeOfferAsync(announcementId, 120m);

        // Przed przetworzeniem outboksa nic nie ma – powiadomienia powstają w tle, nie w komendzie.
        (await owner.GetNotificationsAsync()).ShouldBeEmpty();
        await _factory.ProcessOutboxAsync();

        var received = (await owner.GetNotificationsAsync()).ShouldHaveSingleItem();
        received.Type.ShouldBe("OfferReceived");
        received.AnnouncementId.ShouldBe(announcementId);
        received.ConversationId.ShouldNotBeNull();
        received.IsRead.ShouldBeFalse();
        received.Parameters[NotificationPayload.AnnouncementTitle].ShouldBe("Wymiana baterii w łazience");
        received.Parameters[NotificationPayload.Price].ShouldBe("120.00 PLN");
        (await helper.GetNotificationsAsync()).ShouldBeEmpty(); // autor oferty nic nie dostaje

        var counter = await owner.Client.PostAsJsonAsync(
            $"/api/announcements/{announcementId}/offers/{offerId}/counter", TestApi.OfferBody(100m));
        counter.StatusCode.ShouldBe(HttpStatusCode.Created);
        await _factory.ProcessOutboxAsync();

        var countered = (await helper.GetNotificationsAsync()).ShouldHaveSingleItem();
        countered.Type.ShouldBe("OfferCountered");
        countered.ConversationId.ShouldBe(received.ConversationId);
        countered.Parameters[NotificationPayload.Price].ShouldBe("100.00 PLN");
        (await owner.GetNotificationsAsync()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Accept_NotifiesChosenHelper_AndRejectsOthersWithReason()
    {
        var owner = await _factory.CreateUserAsync();
        var chosen = await _factory.CreateUserAsync();
        var other = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var chosenOffer = await chosen.MakeOfferAsync(announcementId, 120m);
        await other.MakeOfferAsync(announcementId, 130m);

        (await owner.Client.PostEmptyAsync($"/api/announcements/{announcementId}/offers/{chosenOffer}/accept"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _factory.ProcessOutboxAsync();

        (await chosen.GetNotificationsAsync()).ShouldHaveSingleItem().Type.ShouldBe("OfferAccepted");

        var rejected = (await other.GetNotificationsAsync()).ShouldHaveSingleItem();
        rejected.Type.ShouldBe("OfferRejected");
        rejected.Parameters[NotificationPayload.Reason].ShouldBe(NotificationPayload.AnotherOfferAccepted);

        // Właściciel: dwie oferty, nic o własnej akceptacji.
        (await owner.GetNotificationsAsync()).Select(n => n.Type).ShouldBe(new[] { "OfferReceived", "OfferReceived" });
    }

    [Fact]
    public async Task Message_NotifiesOtherParticipant_Edit_NotifiesHelpersButNotOwner()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();

        var start = await helper.Client.PostEmptyAsync($"/api/announcements/{announcementId}/conversations");
        var conversationId = (await start.ReadAsync<CreatedDto>()).Id;
        (await helper.Client.PostAsJsonAsync($"/api/conversations/{conversationId}/messages",
            new { text = "Czy syfon też jest do wymiany?" })).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await owner.Client.PatchAsJsonAsync($"/api/announcements/{announcementId}",
            new { description = "Kran cieknie od tygodnia. Syfon też do wymiany." })).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);
        // Edycja bez zmian – nowej rewizji ani powiadomienia nie ma.
        (await owner.Client.PatchAsJsonAsync($"/api/announcements/{announcementId}",
            new { description = "Kran cieknie od tygodnia. Syfon też do wymiany." })).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);
        await _factory.ProcessOutboxAsync();

        var message = (await owner.GetNotificationsAsync()).ShouldHaveSingleItem();
        message.Type.ShouldBe("MessageReceived");
        message.ConversationId.ShouldBe(conversationId);
        message.Parameters[NotificationPayload.MessagePreview].ShouldBe("Czy syfon też jest do wymiany?");

        var edited = (await helper.GetNotificationsAsync()).ShouldHaveSingleItem();
        edited.Type.ShouldBe("AnnouncementEdited");
        edited.AnnouncementId.ShouldBe(announcementId);
        edited.ConversationId.ShouldBe(conversationId);
        edited.Parameters[NotificationPayload.ChangedFields].ShouldBe("Description");
    }

    [Fact]
    public async Task UnreadCount_MarkOne_MarkAll()
    {
        var owner = await _factory.CreateUserAsync();
        var firstHelper = await _factory.CreateUserAsync();
        var secondHelper = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        await firstHelper.MakeOfferAsync(announcementId, 120m);
        await secondHelper.MakeOfferAsync(announcementId, 110m);
        await _factory.ProcessOutboxAsync();

        (await owner.GetUnreadNotificationCountAsync()).ShouldBe(2);

        var newest = (await owner.GetNotificationsAsync())[0];
        (await owner.Client.PostEmptyAsync($"/api/me/notifications/{newest.Id}/read")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);
        // Drugi raz – nic się nie zmienia.
        (await owner.Client.PostEmptyAsync($"/api/me/notifications/{newest.Id}/read")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        (await owner.GetUnreadNotificationCountAsync()).ShouldBe(1);
        var unread = (await owner.GetNotificationsAsync(unreadOnly: true)).ShouldHaveSingleItem();
        unread.Id.ShouldNotBe(newest.Id);
        (await owner.GetNotificationsAsync()).Single(n => n.Id == newest.Id).ReadAt.ShouldNotBeNull();

        (await owner.Client.PostEmptyAsync("/api/me/notifications/read-all")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);
        (await owner.GetUnreadNotificationCountAsync()).ShouldBe(0);
        (await owner.GetNotificationsAsync(unreadOnly: true)).ShouldBeEmpty();
        (await owner.GetNotificationsAsync()).Count.ShouldBe(2);
    }

    [Fact]
    public async Task MarkAll_WithConversation_MarksOnlyThatThread()
    {
        var owner = await _factory.CreateUserAsync();
        var firstHelper = await _factory.CreateUserAsync();
        var secondHelper = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        await firstHelper.MakeOfferAsync(announcementId, 120m);
        await secondHelper.MakeOfferAsync(announcementId, 110m);
        await _factory.ProcessOutboxAsync();

        var notifications = await owner.GetNotificationsAsync();
        notifications.Count.ShouldBe(2);
        var opened = notifications[0];

        // Obcy wątek (stranger nie ma tam powiadomień) – nic się nie dzieje, 204.
        var stranger = await _factory.CreateUserAsync();
        (await stranger.Client.PostEmptyAsync($"/api/me/notifications/read-all?conversationId={opened.ConversationId}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await owner.GetUnreadNotificationCountAsync()).ShouldBe(2);

        (await owner.Client.PostEmptyAsync($"/api/me/notifications/read-all?conversationId={opened.ConversationId}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await owner.GetUnreadNotificationCountAsync()).ShouldBe(1);
        (await owner.GetNotificationsAsync(unreadOnly: true)).ShouldHaveSingleItem().Id.ShouldBe(notifications[1].Id);
    }

    [Fact]
    public async Task Stranger_CannotSeeOrMarkSomebodyElsesNotification()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var stranger = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        await helper.MakeOfferAsync(announcementId, 120m);
        await _factory.ProcessOutboxAsync();
        var notification = (await owner.GetNotificationsAsync()).ShouldHaveSingleItem();

        (await stranger.GetNotificationsAsync()).ShouldBeEmpty();
        await (await stranger.Client.PostEmptyAsync($"/api/me/notifications/{notification.Id}/read"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotificationNotFound");
        (await stranger.Client.PostEmptyAsync("/api/me/notifications/read-all")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        (await owner.GetUnreadNotificationCountAsync()).ShouldBe(1);
        await (await owner.Client.PostEmptyAsync($"/api/me/notifications/{Guid.NewGuid()}/read"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotificationNotFound");
        (await _factory.CreateClient().GetAsync("/api/me/notifications")).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProcessingAgain_DoesNotDuplicate()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        await helper.MakeOfferAsync(announcementId, 120m);

        await _factory.ProcessOutboxAsync();
        (await _factory.ProcessOutboxAsync()).ShouldBe(0); // nic już nie czeka

        (await owner.GetNotificationsAsync()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Hub_RequiresToken_AcceptsItFromQuery()
    {
        var user = await _factory.CreateUserAsync();
        var token = user.Client.DefaultRequestHeaders.Authorization!.Parameter;
        var anonymous = _factory.CreateClient();

        (await anonymous.PostEmptyAsync("/hubs/notifications/negotiate?negotiateVersion=1")).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);

        // WebSocket nie wyśle nagłówka Authorization – token przychodzi w query (access_token).
        (await anonymous.PostEmptyAsync($"/hubs/notifications/negotiate?negotiateVersion=1&access_token={token}"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
