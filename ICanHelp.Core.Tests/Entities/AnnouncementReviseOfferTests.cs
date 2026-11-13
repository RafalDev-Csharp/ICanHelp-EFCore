using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Events;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;
using static ICanHelp.Core.Tests.Entities.AnnouncementTestData;

namespace ICanHelp.Core.Tests.Entities;

/// <summary>
/// F11 C: autor aktualizuje własną nieaktualną ofertę (ogłoszenie zmieniło się po jej złożeniu);
/// F11 D: także aktualną – dopóki druga strona nie odpowie.
/// </summary>
public class AnnouncementReviseOfferTests
{
    private static void Edit(Announcement announcement, string description = "Kran i syfon w kuchni.")
        => announcement.Update(OwnerId, announcement.CategoryId, announcement.SubcategoryId, announcement.Difficulty,
            announcement.Title, description, announcement.Budget, announcement.Location, Now.AddHours(1));

    private static Offer Revise(Announcement announcement, Offer offer, UserId actorId, decimal price = 180m)
        => announcement.ReviseOffer(offer.Id, Id.New(), actorId, Price.Pln(price), Now.AddDays(3),
            Now.AddDays(3).AddHours(2), "Z syfonem drożej.", Now.AddHours(2));

    [Fact]
    public void ReviseOffer_OutdatedHelperOffer_ReplacesItWithPendingOfferOnCurrentRevision()
    {
        var announcement = CreateAnnouncement();
        var original = announcement.AddHelperOffer(Helper);
        Edit(announcement);

        var revised = Revise(announcement, original, Helper);

        original.Status.ShouldBe(OfferStatus.Revised);
        revised.Status.ShouldBe(OfferStatus.Pending);
        revised.ProposedBy.ShouldBe(OfferParty.Helper);
        revised.HelperId.ShouldBe(Helper);
        revised.ParentOfferId.ShouldBe(original.Id);
        revised.AnnouncementRevision.ShouldBe(announcement.Revision);
        revised.Price.ShouldBe(Price.Pln(180m));
        revised.Message!.Value.ShouldBe("Z syfonem drożej.");
    }

    [Fact]
    public void ReviseOffer_RevisedOfferCanBeAcceptedByOwner()
    {
        var announcement = CreateAnnouncement();
        var original = announcement.AddHelperOffer(Helper);
        Edit(announcement);
        var revised = Revise(announcement, original, Helper);

        announcement.AcceptOffer(revised.Id, OwnerId, Now.AddHours(3));

        announcement.Status.ShouldBe(AnnouncementStatus.Accepted);
        announcement.AcceptedOfferId.ShouldBe(revised.Id);
        original.Status.ShouldBe(OfferStatus.Revised);
    }

    [Fact]
    public void ReviseOffer_OutdatedClientCounter_CanBeRevisedByOwner()
    {
        var announcement = CreateAnnouncement();
        var first = announcement.AddHelperOffer(Helper);
        var clientCounter = announcement.Counter(first, OwnerId);
        Edit(announcement);

        var revised = Revise(announcement, clientCounter, OwnerId, price: 110m);

        clientCounter.Status.ShouldBe(OfferStatus.Revised);
        revised.ProposedBy.ShouldBe(OfferParty.Client);
        revised.HelperId.ShouldBe(Helper);
    }

    [Fact]
    public void ReviseOffer_RaisesOfferRevised()
    {
        var announcement = CreateAnnouncement();
        var original = announcement.AddHelperOffer(Helper);
        Edit(announcement);
        announcement.ClearDomainEvents();

        var revised = Revise(announcement, original, Helper);

        var e = announcement.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<OfferRevised>();
        e.AnnouncementId.ShouldBe(announcement.Id.Value);
        e.OfferId.ShouldBe(revised.Id.Value);
        e.ParentOfferId.ShouldBe(original.Id.Value);
        e.OwnerId.ShouldBe(OwnerId.Value);
        e.HelperId.ShouldBe(Helper.Value);
        e.ProposedBy.ShouldBe(OfferParty.Helper);
        e.Amount.ShouldBe(180m);
        e.Currency.ShouldBe("PLN");
    }

    [Fact]
    public void ReviseOffer_UpToDateOffer_ChangesTermsBeforeTheAnswer()
    {
        // F11 D: autor zmienia każdą swoją oczekującą propozycję, nie tylko nieaktualną.
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);

        var revised = Revise(announcement, offer, Helper, price: 90m);

        offer.Status.ShouldBe(OfferStatus.Revised);
        revised.Status.ShouldBe(OfferStatus.Pending);
        revised.Price.ShouldBe(Price.Pln(90m));
        revised.AnnouncementRevision.ShouldBe(announcement.Revision);
        announcement.Offers.Count(o => o.Status == OfferStatus.Pending).ShouldBe(1);
    }

    [Fact]
    public void ReviseOffer_UpToDateClientCounter_CanBeChangedByOwner()
    {
        var announcement = CreateAnnouncement();
        var first = announcement.AddHelperOffer(Helper);
        var clientCounter = announcement.Counter(first, OwnerId);

        var revised = Revise(announcement, clientCounter, OwnerId, price: 105m);

        clientCounter.Status.ShouldBe(OfferStatus.Revised);
        revised.ProposedBy.ShouldBe(OfferParty.Client);
    }

    [Fact]
    public void ReviseOffer_ByOtherSide_Throws()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);
        Edit(announcement);

        Should.Throw<DomainRuleException>(() => Revise(announcement, offer, OwnerId));

        offer.Status.ShouldBe(OfferStatus.Pending);
    }

    [Theory]
    [MemberData(nameof(NonParticipants))]
    public void ReviseOffer_ByNonParticipant_Throws(UserId actorId)
    {
        var announcement = CreateAnnouncement();
        announcement.AddHelperOffer(OtherHelper);
        var offer = announcement.AddHelperOffer(Helper);
        Edit(announcement);

        Should.Throw<DomainRuleException>(() => Revise(announcement, offer, actorId));
    }

    [Fact]
    public void ReviseOffer_NotPendingOffer_Throws()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);
        Edit(announcement);
        Revise(announcement, offer, Helper);

        Should.Throw<DomainRuleException>(() => Revise(announcement, offer, Helper));
    }

    [Fact]
    public void ReviseOffer_InvalidData_KeepsOriginalPending()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);
        Edit(announcement);

        Should.Throw<DomainRuleException>(() => Revise(announcement, offer, Helper, price: 0m));

        offer.Status.ShouldBe(OfferStatus.Pending);
        announcement.Offers.Count.ShouldBe(1);
    }

    [Fact]
    public void ReviseOffer_UnknownOffer_ThrowsOfferNotFound()
    {
        var announcement = CreateAnnouncement();

        Should.Throw<OfferNotFoundException>(() => announcement.ReviseOffer(Id.New(), Id.New(), Helper,
            Price.Pln(100m), Now.AddDays(1), Now.AddDays(1).AddHours(1), null, Now));
    }

    [Fact]
    public void ReviseOffer_WhenAnnouncementNotOpen_Throws()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);
        Edit(announcement);
        announcement.Cancel(OwnerId);

        Should.Throw<DomainRuleException>(() => Revise(announcement, offer, Helper));
    }

    public static TheoryData<UserId> NonParticipants => new() { Stranger, OtherHelper };
}
