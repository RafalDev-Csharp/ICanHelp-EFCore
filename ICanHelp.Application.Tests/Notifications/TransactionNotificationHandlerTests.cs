using ICanHelp.Application.Notifications;
using ICanHelp.Application.Tests.Fakes;
using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Events;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Tests.Notifications;

/// <summary>Krok 13: kto dostaje powiadomienie o zdarzeniu (zawsze druga strona, nigdy autor) i idempotencja.</summary>
public class TransactionNotificationHandlerTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryAnnouncementRepository _announcements = new();
    private readonly InMemoryConversationRepository _conversations = new();
    private readonly InMemoryNotificationRepository _notifications = new();
    private readonly RecordingNotificationSender _sender = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly TransactionNotificationHandler _handler;

    private readonly UserId _owner = UserId.New();
    private readonly UserId _helper = UserId.New();
    private readonly UserId _otherHelper = UserId.New();
    private readonly Announcement _announcement;
    private readonly Conversation _conversation;

    public TransactionNotificationHandlerTests()
    {
        _announcement = new Announcement(Id.New(), _owner, Id.New(), Id.New(), DifficultyLevel.Easy, "Cieknący kran",
            "Kran w kuchni cieknie od tygodnia.", Price.Pln(150), new Location(52.2297, 21.0122), Now);
        _announcements.Announcements.Add(_announcement);

        _conversation = _announcement.StartConversation(Id.New(), _helper, Now);
        _conversations.Conversations.Add(_conversation);

        var publisher = new NotificationPublisher(_notifications, _unitOfWork, _sender);
        _handler = new TransactionNotificationHandler(_announcements, _conversations, publisher);
    }

    [Fact]
    public async Task OfferMade_NotifiesOwner_WithTitlePriceAndConversation()
    {
        var offerId = Guid.NewGuid();

        await _handler.HandleAsync(Guid.NewGuid(),
            new OfferMade(_announcement.Id, offerId, _owner, _helper, 120m, "PLN", Now.AddMinutes(1)));

        var notification = _notifications.Notifications.ShouldHaveSingleItem();
        notification.UserId.ShouldBe(_owner);
        notification.Type.ShouldBe(NotificationType.OfferReceived);
        notification.AnnouncementId.ShouldBe(_announcement.Id);
        notification.ConversationId.ShouldBe(_conversation.Id);
        notification.CreatedAt.ShouldBe(Now.AddMinutes(1));
        notification.ReadAt.ShouldBeNull();

        var parameters = NotificationPayload.Deserialize(notification.Payload);
        parameters[NotificationPayload.AnnouncementTitle].ShouldBe("Cieknący kran");
        parameters[NotificationPayload.Price].ShouldBe("120.00 PLN");

        var sent = _sender.Sent.ShouldHaveSingleItem();
        sent.UserId.ShouldBe(_owner.Value);
        sent.Notification.Id.ShouldBe(notification.Id.Value);
        sent.Notification.Type.ShouldBe("OfferReceived");
    }

    [Theory]
    [InlineData(OfferParty.Client, false)] // właściciel kontruje → pomocnik
    [InlineData(OfferParty.Helper, true)]  // pomocnik kontruje → właściciel
    public async Task OfferCountered_NotifiesOtherSideThanAuthor(OfferParty proposedBy, bool ownerIsRecipient)
    {
        await _handler.HandleAsync(Guid.NewGuid(), new OfferCountered(_announcement.Id, Guid.NewGuid(), Guid.NewGuid(),
            _owner, _helper, proposedBy, 100m, "PLN", Now));

        var notification = _notifications.Notifications.ShouldHaveSingleItem();
        notification.UserId.ShouldBe(ownerIsRecipient ? _owner : _helper);
        notification.Type.ShouldBe(NotificationType.OfferCountered);
    }

    [Theory]
    [InlineData(OfferParty.Client, false)] // właściciel aktualizuje swoją kontrofertę → pomocnik
    [InlineData(OfferParty.Helper, true)]  // pomocnik aktualizuje swoją ofertę → właściciel
    public async Task OfferRevised_NotifiesOtherSideThanAuthor(OfferParty proposedBy, bool ownerIsRecipient)
    {
        await _handler.HandleAsync(Guid.NewGuid(), new OfferRevised(_announcement.Id, Guid.NewGuid(), Guid.NewGuid(),
            _owner, _helper, proposedBy, 180m, "PLN", Now));

        var notification = _notifications.Notifications.ShouldHaveSingleItem();
        notification.UserId.ShouldBe(ownerIsRecipient ? _owner : _helper);
        notification.Type.ShouldBe(NotificationType.OfferRevised);
    }

    [Theory]
    [InlineData(OfferParty.Helper, false)] // właściciel przyjął ofertę pomocnika → pomocnik
    [InlineData(OfferParty.Client, true)]  // pomocnik przyjął kontrofertę właściciela → właściciel
    public async Task OfferAccepted_NotifiesAuthorOfOffer(OfferParty proposedBy, bool ownerIsRecipient)
    {
        await _handler.HandleAsync(Guid.NewGuid(),
            new OfferAccepted(_announcement.Id, Guid.NewGuid(), _owner, _helper, proposedBy, Now));

        _notifications.Notifications.ShouldHaveSingleItem().UserId.ShouldBe(ownerIsRecipient ? _owner : _helper);
    }

    [Fact]
    public async Task OfferRejected_Automatically_NotifiesHelperWithReason()
    {
        // Czekała kontroferta właściciela (autor = Client), ale właściciel wybrał kogoś innego.
        await _handler.HandleAsync(Guid.NewGuid(), new OfferRejected(_announcement.Id, Guid.NewGuid(), _owner, _helper,
            OfferParty.Client, AnotherOfferAccepted: true, Now));

        var notification = _notifications.Notifications.ShouldHaveSingleItem();
        notification.UserId.ShouldBe(_helper);
        notification.Type.ShouldBe(NotificationType.OfferRejected);
        NotificationPayload.Deserialize(notification.Payload)[NotificationPayload.Reason]
            .ShouldBe(NotificationPayload.AnotherOfferAccepted);
    }

    [Fact]
    public async Task OfferWithdrawn_ByHelper_NotifiesOwner()
    {
        await _handler.HandleAsync(Guid.NewGuid(),
            new OfferWithdrawn(_announcement.Id, Guid.NewGuid(), _owner, _helper, OfferParty.Helper, Now));

        _notifications.Notifications.ShouldHaveSingleItem().UserId.ShouldBe(_owner);
    }

    [Fact]
    public async Task MessageSent_NotifiesOtherParticipant_NeverSender()
    {
        await _handler.HandleAsync(Guid.NewGuid(), new MessageSent(_conversation.Id, _announcement.Id, Guid.NewGuid(),
            _owner, _helper, _owner, "Tak, syfon też.", Now));

        var notification = _notifications.Notifications.ShouldHaveSingleItem();
        notification.UserId.ShouldBe(_helper);
        notification.Type.ShouldBe(NotificationType.MessageReceived);
        notification.ConversationId.ShouldBe(_conversation.Id);
        NotificationPayload.Deserialize(notification.Payload)[NotificationPayload.MessagePreview]
            .ShouldBe("Tak, syfon też.");
    }

    [Fact]
    public async Task AnnouncementEdited_NotifiesEveryHelperWithConversation_NotOwner()
    {
        var otherConversation = _announcement.StartConversation(Id.New(), _otherHelper, Now);
        _conversations.Conversations.Add(otherConversation);

        await _handler.HandleAsync(Guid.NewGuid(), new AnnouncementEdited(_announcement.Id, _owner, 2,
            AnnouncementFields.Description | AnnouncementFields.Budget, Now));

        _notifications.Notifications.Select(n => n.UserId).ShouldBe(new[] { _helper, _otherHelper }, ignoreOrder: true);
        _notifications.Notifications.ShouldAllBe(n => n.Type == NotificationType.AnnouncementEdited);
        _notifications.Notifications.Single(n => n.UserId == _otherHelper).ConversationId.ShouldBe(otherConversation.Id);

        var parameters = NotificationPayload.Deserialize(_notifications.Notifications[0].Payload);
        parameters[NotificationPayload.Revision].ShouldBe("2");
        parameters[NotificationPayload.ChangedFields].ShouldBe("Description, Budget");
    }

    [Fact]
    public async Task SameEventTwice_GivesOneNotification_AndOneDelivery()
    {
        var eventId = Guid.NewGuid();
        var made = new OfferMade(_announcement.Id, Guid.NewGuid(), _owner, _helper, 120m, "PLN", Now);

        await _handler.HandleAsync(eventId, made);
        await _handler.HandleAsync(eventId, made);

        _notifications.Notifications.ShouldHaveSingleItem().SourceEventId.ShouldBe(eventId);
        _sender.Sent.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AnnouncementGone_NothingIsSent()
    {
        await _handler.HandleAsync(Guid.NewGuid(),
            new OfferMade(Guid.NewGuid(), Guid.NewGuid(), _owner, _helper, 120m, "PLN", Now));

        _notifications.Notifications.ShouldBeEmpty();
        _sender.Sent.ShouldBeEmpty();
        _unitOfWork.Executions.ShouldBe(0);
    }

    [Fact]
    public async Task Publisher_NeverNotifiesActor()
    {
        var publisher = new NotificationPublisher(_notifications, _unitOfWork, _sender);

        await publisher.PublishAsync(Guid.NewGuid(), _owner, Now,
            [new NotificationDraft(_owner, NotificationType.OfferReceived, null, null, new Dictionary<string, string>())],
            CancellationToken.None);

        _notifications.Notifications.ShouldBeEmpty();
        _sender.Sent.ShouldBeEmpty();
    }
}
