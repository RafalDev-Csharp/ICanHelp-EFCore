using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Notifications;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Events;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;
using static ICanHelp.Core.Tests.Entities.AnnouncementTestData;

namespace ICanHelp.Core.Tests.Entities;

/// <summary>Krok 13: agregaty podnoszą zdarzenia domenowe (źródło powiadomień) z czasem podanym parametrem.</summary>
public class DomainEventsTests
{
    /// <summary>Ogłoszenie bez zdarzenia „opublikowane” – żeby w teście zostały tylko zdarzenia badanej akcji.</summary>
    private static Announcement CreateCleared()
    {
        var announcement = CreateAnnouncement();
        announcement.ClearDomainEvents();
        return announcement;
    }

    // ---------- ogłoszenie ----------

    [Fact]
    public void NewAnnouncement_RaisesPublished()
    {
        var announcement = CreateAnnouncement();

        var published = announcement.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<AnnouncementPublished>();
        published.AnnouncementId.ShouldBe(announcement.Id.Value);
        published.OwnerId.ShouldBe(OwnerId.Value);
        published.CategoryId.ShouldBe(announcement.CategoryId.Value);
        published.SubcategoryId.ShouldBe(announcement.SubcategoryId.Value);
        published.Latitude.ShouldBe(announcement.Location.Latitude);
        published.Longitude.ShouldBe(announcement.Location.Longitude);
        published.OccurredAt.ShouldBe(Now);
    }

    [Fact]
    public void ClearDomainEvents_RemovesAll()
    {
        var announcement = CreateAnnouncement();

        announcement.ClearDomainEvents();

        announcement.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Update_WithChanges_RaisesEditedWithRevisionAndFields()
    {
        var announcement = CreateCleared();

        announcement.Update(OwnerId, announcement.CategoryId, announcement.SubcategoryId, announcement.Difficulty,
            "Nowy tytuł", announcement.Description, Price.Pln(200), announcement.Location, Now.AddHours(1));

        var edited = announcement.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<AnnouncementEdited>();
        edited.AnnouncementId.ShouldBe(announcement.Id.Value);
        edited.OwnerId.ShouldBe(OwnerId.Value);
        edited.Revision.ShouldBe(1);
        edited.ChangedFields.ShouldBe(AnnouncementFields.Title | AnnouncementFields.Budget);
        edited.OccurredAt.ShouldBe(Now.AddHours(1));
    }

    [Fact]
    public void Update_WithoutChanges_RaisesNothing()
    {
        var announcement = CreateCleared();

        announcement.Update(OwnerId, announcement.CategoryId, announcement.SubcategoryId, announcement.Difficulty,
            announcement.Title, announcement.Description, announcement.Budget, announcement.Location, Now.AddHours(1));

        announcement.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void AddAndRemoveImage_EachRaisesEditedWithImagesFlag()
    {
        var announcement = CreateCleared();
        var imageId = Id.New();

        announcement.AddImage(OwnerId, imageId, "kran.jpg", ".jpg", "a/b.jpg", Now.AddMinutes(10));
        announcement.RemoveImage(OwnerId, imageId, Now.AddMinutes(20));

        var events = announcement.DomainEvents.Cast<AnnouncementEdited>().ToList();
        events.Count.ShouldBe(2);
        events.ShouldAllBe(e => e.ChangedFields == AnnouncementFields.Images);
        events.Select(e => e.Revision).ShouldBe(new[] { 1, 2 });
        events[1].OccurredAt.ShouldBe(Now.AddMinutes(20));
    }

    [Fact]
    public void FailedRule_RaisesNothing()
    {
        var announcement = CreateCleared();

        Should.Throw<DomainRuleException>(() => announcement.AddHelperOffer(OwnerId));

        announcement.DomainEvents.ShouldBeEmpty();
    }

    // ---------- oferty ----------

    [Fact]
    public void AddOffer_RaisesOfferMade()
    {
        var announcement = CreateCleared();

        var offer = announcement.AddHelperOffer(Helper, price: 120m);

        var made = announcement.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<OfferMade>();
        made.OfferId.ShouldBe(offer.Id.Value);
        made.AnnouncementId.ShouldBe(announcement.Id.Value);
        made.OwnerId.ShouldBe(OwnerId.Value);
        made.HelperId.ShouldBe(Helper.Value);
        made.Amount.ShouldBe(120m);
        made.Currency.ShouldBe("PLN");
        made.OccurredAt.ShouldBe(Now);
    }

    [Fact]
    public void CounterOffer_ByOwner_RaisesOfferCounteredProposedByClient()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);
        announcement.ClearDomainEvents();

        var counter = announcement.Counter(offer, OwnerId, price: 100m);

        var countered = announcement.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<OfferCountered>();
        countered.OfferId.ShouldBe(counter.Id.Value);
        countered.ParentOfferId.ShouldBe(offer.Id.Value);
        countered.HelperId.ShouldBe(Helper.Value);
        countered.ProposedBy.ShouldBe(OfferParty.Client);
        countered.Amount.ShouldBe(100m);
        countered.OccurredAt.ShouldBe(Now.AddHours(1));
    }

    [Fact]
    public void AcceptOffer_RaisesAcceptedAndRejectedForOtherPendingOffers()
    {
        var announcement = CreateAnnouncement();
        var chosen = announcement.AddHelperOffer(Helper);
        var other = announcement.AddHelperOffer(OtherHelper);
        announcement.ClearDomainEvents();

        announcement.AcceptOffer(chosen.Id, OwnerId, Now.AddHours(3));

        var accepted = announcement.DomainEvents.OfType<OfferAccepted>().ShouldHaveSingleItem();
        accepted.OfferId.ShouldBe(chosen.Id.Value);
        accepted.HelperId.ShouldBe(Helper.Value);
        accepted.ProposedBy.ShouldBe(OfferParty.Helper);
        accepted.OccurredAt.ShouldBe(Now.AddHours(3));

        var rejected = announcement.DomainEvents.OfType<OfferRejected>().ShouldHaveSingleItem();
        rejected.OfferId.ShouldBe(other.Id.Value);
        rejected.HelperId.ShouldBe(OtherHelper.Value);
        rejected.AnotherOfferAccepted.ShouldBeTrue();
        announcement.DomainEvents.Count.ShouldBe(2);
    }

    [Fact]
    public void RejectOffer_RaisesRejectedNotAutomatic()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);
        announcement.ClearDomainEvents();

        announcement.RejectOffer(offer.Id, OwnerId, Now.AddHours(2));

        var rejected = announcement.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<OfferRejected>();
        rejected.OfferId.ShouldBe(offer.Id.Value);
        rejected.ProposedBy.ShouldBe(OfferParty.Helper);
        rejected.AnotherOfferAccepted.ShouldBeFalse();
        rejected.OccurredAt.ShouldBe(Now.AddHours(2));
    }

    [Fact]
    public void WithdrawOffer_RaisesWithdrawn()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);
        announcement.ClearDomainEvents();

        announcement.WithdrawOffer(offer.Id, Helper, Now.AddHours(2));

        var withdrawn = announcement.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<OfferWithdrawn>();
        withdrawn.OfferId.ShouldBe(offer.Id.Value);
        withdrawn.ProposedBy.ShouldBe(OfferParty.Helper);
        withdrawn.OccurredAt.ShouldBe(Now.AddHours(2));
    }

    // ---------- wiadomości ----------

    [Fact]
    public void SendMessage_RaisesMessageSentWithBothParticipants()
    {
        var announcement = CreateAnnouncement();
        var conversation = announcement.StartConversation(Id.New(), Helper, Now);

        var message = conversation.SendMessage(Id.New(), Helper, "Czy syfon też?", announcement, Now.AddMinutes(5));

        var sent = conversation.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<MessageSent>();
        sent.ConversationId.ShouldBe(conversation.Id.Value);
        sent.AnnouncementId.ShouldBe(announcement.Id.Value);
        sent.MessageId.ShouldBe(message.Id.Value);
        sent.ClientId.ShouldBe(OwnerId.Value);
        sent.HelperId.ShouldBe(Helper.Value);
        sent.SenderId.ShouldBe(Helper.Value);
        sent.Text.ShouldBe("Czy syfon też?");
        sent.OccurredAt.ShouldBe(Now.AddMinutes(5));
    }

    [Fact]
    public void NewConversation_RaisesNothing()
    {
        var announcement = CreateAnnouncement();

        var conversation = announcement.StartConversation(Id.New(), Helper, Now);

        conversation.DomainEvents.ShouldBeEmpty();
    }

    // ---------- powiadomienie ----------

    [Fact]
    public void Notification_MarkAsRead_OnlyRecipient_FirstTimeStays()
    {
        var notification = new Notification(Id.New(), Helper, NotificationType.OfferAccepted, "{}", Id.New(), null,
            Guid.NewGuid(), Now);

        Should.Throw<DomainRuleException>(() => notification.MarkAsRead(Stranger, Now.AddMinutes(1)));
        notification.ReadAt.ShouldBeNull();

        notification.MarkAsRead(Helper, Now.AddMinutes(2));
        notification.MarkAsRead(Helper, Now.AddMinutes(5));

        notification.ReadAt.ShouldBe(Now.AddMinutes(2));
    }

    [Fact]
    public void Notification_WithoutSourceEvent_Throws()
        => Should.Throw<DomainRuleException>(() => new Notification(Id.New(), Helper, NotificationType.OfferAccepted,
            "{}", null, null, Guid.Empty, Now));
}
