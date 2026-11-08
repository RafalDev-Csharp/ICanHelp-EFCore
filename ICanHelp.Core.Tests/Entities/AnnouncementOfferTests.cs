using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;
using static ICanHelp.Core.Tests.Entities.AnnouncementTestData;

namespace ICanHelp.Core.Tests.Entities;

/// <summary>Negocjacje: oferty, kontroferty, akceptacja, odrzucenie, wycofanie.</summary>
public class AnnouncementOfferTests
{
    // ---------- AddOffer ----------

    [Fact]
    public void AddOffer_ByHelper_AddsPendingOfferProposedByHelper()
    {
        var announcement = CreateAnnouncement();

        var offer = announcement.AddHelperOffer(Helper, price: 120m);

        offer.Status.ShouldBe(OfferStatus.Pending);
        offer.ProposedBy.ShouldBe(OfferParty.Helper);
        offer.HelperId.ShouldBe(Helper);
        offer.AnnouncementId.ShouldBe(announcement.Id);
        offer.Price.ShouldBe(Price.Pln(120));
        offer.ParentOfferId.ShouldBeNull();
        offer.CreatedAt.ShouldBe(Now);
        announcement.Offers.ShouldHaveSingleItem().ShouldBeSameAs(offer);
    }

    [Fact]
    public void AddOffer_ByOwner_Throws()
    {
        var announcement = CreateAnnouncement();

        Should.Throw<DomainRuleException>(() => announcement.AddHelperOffer(OwnerId));

        announcement.Offers.ShouldBeEmpty();
    }

    [Fact]
    public void AddOffer_HelperAlreadyHasPendingOffer_Throws()
    {
        var announcement = CreateAnnouncement();
        announcement.AddHelperOffer(Helper);

        Should.Throw<DomainRuleException>(() => announcement.AddHelperOffer(Helper, price: 90m));

        announcement.Offers.Count.ShouldBe(1);
    }

    [Fact]
    public void AddOffer_DifferentHelpers_BothOffersArePending()
    {
        var announcement = CreateAnnouncement();

        announcement.AddHelperOffer(Helper);
        announcement.AddHelperOffer(OtherHelper);

        announcement.Offers.Count.ShouldBe(2);
        announcement.Offers.ShouldAllBe(o => o.Status == OfferStatus.Pending);
    }

    [Fact]
    public void AddOffer_AfterOwnOfferWasWithdrawn_IsAllowed()
    {
        var announcement = CreateAnnouncement();
        var first = announcement.AddHelperOffer(Helper);
        announcement.WithdrawOffer(first.Id, Helper, Now);

        var second = announcement.AddHelperOffer(Helper, price: 110m);

        second.Status.ShouldBe(OfferStatus.Pending);
        announcement.Offers.Count.ShouldBe(2);
    }

    [Fact]
    public void AddOffer_AfterOwnOfferWasRejected_IsAllowed()
    {
        var announcement = CreateAnnouncement();
        var first = announcement.AddHelperOffer(Helper);
        announcement.RejectOffer(first.Id, OwnerId, Now);

        var second = announcement.AddHelperOffer(Helper, price: 110m);

        second.Status.ShouldBe(OfferStatus.Pending);
    }

    [Fact]
    public void AddOffer_ZeroPrice_Throws()
    {
        var announcement = CreateAnnouncement();

        Should.Throw<DomainRuleException>(() => announcement.AddHelperOffer(Helper, price: 0m));

        announcement.Offers.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0)]   // koniec == początek
    [InlineData(-1)]  // koniec przed początkiem
    public void AddOffer_EndNotAfterStart_Throws(int endOffsetHours)
    {
        var announcement = CreateAnnouncement();
        var start = Now.AddDays(1);

        Should.Throw<DomainRuleException>(() => announcement.AddOffer(Id.New(), Helper, Price.Pln(100),
            start, start.AddHours(endOffsetHours), null, Now));

        announcement.Offers.ShouldBeEmpty();
    }

    [Fact]
    public void AddOffer_StartInThePast_Throws()
    {
        var announcement = CreateAnnouncement();

        Should.Throw<DomainRuleException>(() => announcement.AddOffer(Id.New(), Helper, Price.Pln(100),
            Now.AddMinutes(-1), Now.AddHours(2), null, Now));

        announcement.Offers.ShouldBeEmpty();
    }

    [Fact]
    public void AddOffer_StartingExactlyNow_IsAllowed()
    {
        var announcement = CreateAnnouncement();

        var offer = announcement.AddOffer(Id.New(), Helper, Price.Pln(100), Now, Now.AddHours(1), null, Now);

        offer.Status.ShouldBe(OfferStatus.Pending);
    }

    [Theory]
    [InlineData(AnnouncementStatus.Accepted)]
    [InlineData(AnnouncementStatus.InProgress)]
    [InlineData(AnnouncementStatus.Completed)]
    [InlineData(AnnouncementStatus.Cancelled)]
    public void AddOffer_WhenAnnouncementNotOpen_Throws(AnnouncementStatus status)
    {
        var announcement = InStatus(status);

        Should.Throw<DomainRuleException>(() => announcement.AddHelperOffer(OtherHelper));
    }

    // ---------- CounterOffer ----------

    [Fact]
    public void CounterOffer_ByClient_MarksOriginalAsCounteredAndAddsPendingCounter()
    {
        var announcement = CreateAnnouncement();
        var original = announcement.AddHelperOffer(Helper, price: 150m);

        var counter = announcement.Counter(original, OwnerId, price: 100m);

        original.Status.ShouldBe(OfferStatus.Countered);
        counter.Status.ShouldBe(OfferStatus.Pending);
        counter.ProposedBy.ShouldBe(OfferParty.Client);
        counter.HelperId.ShouldBe(Helper);
        counter.ParentOfferId.ShouldBe(original.Id);
        counter.Price.ShouldBe(Price.Pln(100));
        counter.CreatedAt.ShouldBe(Now.AddHours(1));
        announcement.Offers.Count.ShouldBe(2);
        announcement.Status.ShouldBe(AnnouncementStatus.Open);
    }

    [Fact]
    public void CounterOffer_ByHelperOnClientCounter_AddsCounterProposedByHelper()
    {
        var announcement = CreateAnnouncement();
        var first = announcement.AddHelperOffer(Helper, price: 150m);
        var clientCounter = announcement.Counter(first, OwnerId, price: 100m);

        var helperCounter = announcement.Counter(clientCounter, Helper, price: 125m);

        clientCounter.Status.ShouldBe(OfferStatus.Countered);
        helperCounter.Status.ShouldBe(OfferStatus.Pending);
        helperCounter.ProposedBy.ShouldBe(OfferParty.Helper);
        helperCounter.ParentOfferId.ShouldBe(clientCounter.Id);
    }

    [Fact]
    public void CounterOffer_ByHelperOnOwnOffer_Throws()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);

        Should.Throw<DomainRuleException>(() => announcement.Counter(offer, Helper));

        offer.Status.ShouldBe(OfferStatus.Pending);
        announcement.Offers.Count.ShouldBe(1);
    }

    [Fact]
    public void CounterOffer_ByClientOnOwnCounter_Throws()
    {
        var announcement = CreateAnnouncement();
        var first = announcement.AddHelperOffer(Helper);
        var clientCounter = announcement.Counter(first, OwnerId);

        Should.Throw<DomainRuleException>(() => announcement.Counter(clientCounter, OwnerId));

        clientCounter.Status.ShouldBe(OfferStatus.Pending);
    }

    [Theory]
    [MemberData(nameof(NonParticipants))]
    public void CounterOffer_ByNonParticipant_Throws(UserId actorId)
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);
        announcement.AddHelperOffer(OtherHelper);

        Should.Throw<DomainRuleException>(() => announcement.Counter(offer, actorId));

        offer.Status.ShouldBe(OfferStatus.Pending);
    }

    [Fact]
    public void CounterOffer_OnAlreadyCounteredOffer_Throws()
    {
        var announcement = CreateAnnouncement();
        var first = announcement.AddHelperOffer(Helper);
        announcement.Counter(first, OwnerId);

        Should.Throw<DomainRuleException>(() => announcement.Counter(first, OwnerId));
    }

    [Fact]
    public void CounterOffer_UnknownOffer_ThrowsOfferNotFound()
    {
        var announcement = CreateAnnouncement();
        announcement.AddHelperOffer(Helper);
        var unknownId = Id.New();

        var exception = Should.Throw<OfferNotFoundException>(() => announcement.CounterOffer(unknownId, Id.New(),
            OwnerId, Price.Pln(100), Now.AddDays(2), Now.AddDays(2).AddHours(1), null, Now));

        exception.OfferId.ShouldBe(unknownId.Value);
    }

    [Fact]
    public void CounterOffer_InvalidData_KeepsOriginalPending()
    {
        var announcement = CreateAnnouncement();
        var original = announcement.AddHelperOffer(Helper);

        // Koniec przed początkiem – nowa oferta jest nieprawidłowa.
        Should.Throw<DomainRuleException>(() => announcement.CounterOffer(original.Id, Id.New(), OwnerId,
            Price.Pln(100), Now.AddDays(2), Now.AddDays(1), null, Now));

        original.Status.ShouldBe(OfferStatus.Pending);
        announcement.Offers.ShouldHaveSingleItem();
    }

    [Fact]
    public void CounterOffer_WhenAnnouncementNotOpen_Throws()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);
        announcement.Cancel(OwnerId);

        Should.Throw<DomainRuleException>(() => announcement.Counter(offer, OwnerId));

        offer.Status.ShouldBe(OfferStatus.Pending);
    }

    // ---------- AcceptOffer ----------

    [Fact]
    public void AcceptOffer_ByClient_AcceptsOfferAndAnnouncement()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);

        announcement.AcceptOffer(offer.Id, OwnerId, Now);

        offer.Status.ShouldBe(OfferStatus.Accepted);
        announcement.Status.ShouldBe(AnnouncementStatus.Accepted);
        announcement.AcceptedOfferId.ShouldBe(offer.Id);
    }

    [Fact]
    public void AcceptOffer_RejectsOtherPendingOffers_AndKeepsFinishedOnesUnchanged()
    {
        var announcement = CreateAnnouncement();
        var third = UserId.New();

        var chosen = announcement.AddHelperOffer(Helper);
        var otherFirst = announcement.AddHelperOffer(OtherHelper);
        var otherCounter = announcement.Counter(otherFirst, OwnerId);   // otherFirst -> Countered
        var withdrawn = announcement.AddHelperOffer(third);
        announcement.WithdrawOffer(withdrawn.Id, third, Now);

        announcement.AcceptOffer(chosen.Id, OwnerId, Now);

        chosen.Status.ShouldBe(OfferStatus.Accepted);
        otherCounter.Status.ShouldBe(OfferStatus.Rejected);
        otherFirst.Status.ShouldBe(OfferStatus.Countered);
        withdrawn.Status.ShouldBe(OfferStatus.Withdrawn);
    }

    [Fact]
    public void AcceptOffer_ByHelperOnClientCounter_AcceptsCounter()
    {
        var announcement = CreateAnnouncement();
        var first = announcement.AddHelperOffer(Helper, price: 150m);
        var clientCounter = announcement.Counter(first, OwnerId, price: 100m);

        announcement.AcceptOffer(clientCounter.Id, Helper, Now);

        clientCounter.Status.ShouldBe(OfferStatus.Accepted);
        first.Status.ShouldBe(OfferStatus.Countered);
        announcement.AcceptedOfferId.ShouldBe(clientCounter.Id);
        announcement.Status.ShouldBe(AnnouncementStatus.Accepted);
    }

    [Fact]
    public void AcceptOffer_ByHelperOnOwnOffer_Throws()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);

        Should.Throw<DomainRuleException>(() => announcement.AcceptOffer(offer.Id, Helper, Now));

        offer.Status.ShouldBe(OfferStatus.Pending);
        announcement.Status.ShouldBe(AnnouncementStatus.Open);
        announcement.AcceptedOfferId.ShouldBeNull();
    }

    [Fact]
    public void AcceptOffer_ByClientOnOwnCounter_Throws()
    {
        var announcement = CreateAnnouncement();
        var first = announcement.AddHelperOffer(Helper);
        var clientCounter = announcement.Counter(first, OwnerId);

        Should.Throw<DomainRuleException>(() => announcement.AcceptOffer(clientCounter.Id, OwnerId, Now));

        clientCounter.Status.ShouldBe(OfferStatus.Pending);
    }

    [Theory]
    [MemberData(nameof(NonParticipants))]
    public void AcceptOffer_ByNonParticipant_Throws(UserId actorId)
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);
        announcement.AddHelperOffer(OtherHelper);

        Should.Throw<DomainRuleException>(() => announcement.AcceptOffer(offer.Id, actorId, Now));

        offer.Status.ShouldBe(OfferStatus.Pending);
        announcement.Status.ShouldBe(AnnouncementStatus.Open);
    }

    [Fact]
    public void AcceptOffer_CounteredOffer_Throws()
    {
        var announcement = CreateAnnouncement();
        var first = announcement.AddHelperOffer(Helper);
        announcement.Counter(first, OwnerId);

        Should.Throw<DomainRuleException>(() => announcement.AcceptOffer(first.Id, OwnerId, Now));
    }

    [Fact]
    public void AcceptOffer_WithdrawnOffer_Throws()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);
        announcement.WithdrawOffer(offer.Id, Helper, Now);

        Should.Throw<DomainRuleException>(() => announcement.AcceptOffer(offer.Id, OwnerId, Now));

        announcement.Status.ShouldBe(AnnouncementStatus.Open);
    }

    [Fact]
    public void AcceptOffer_WhenAnotherOfferAlreadyAccepted_Throws()
    {
        var announcement = CreateAnnouncement();
        var first = announcement.AddHelperOffer(Helper);
        var second = announcement.AddHelperOffer(OtherHelper);
        announcement.AcceptOffer(first.Id, OwnerId, Now);

        Should.Throw<DomainRuleException>(() => announcement.AcceptOffer(second.Id, OwnerId, Now));

        announcement.AcceptedOfferId.ShouldBe(first.Id);
    }

    [Fact]
    public void AcceptOffer_UnknownOffer_ThrowsOfferNotFound()
    {
        var announcement = CreateAnnouncement();
        announcement.AddHelperOffer(Helper);

        Should.Throw<OfferNotFoundException>(() => announcement.AcceptOffer(Id.New(), OwnerId, Now));
    }

    // ---------- RejectOffer ----------

    [Fact]
    public void RejectOffer_ByClient_RejectsOfferAndAnnouncementStaysOpen()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);
        var other = announcement.AddHelperOffer(OtherHelper);

        announcement.RejectOffer(offer.Id, OwnerId, Now);

        offer.Status.ShouldBe(OfferStatus.Rejected);
        other.Status.ShouldBe(OfferStatus.Pending);
        announcement.Status.ShouldBe(AnnouncementStatus.Open);
    }

    [Fact]
    public void RejectOffer_ByHelperOnClientCounter_RejectsCounter()
    {
        var announcement = CreateAnnouncement();
        var first = announcement.AddHelperOffer(Helper);
        var clientCounter = announcement.Counter(first, OwnerId);

        announcement.RejectOffer(clientCounter.Id, Helper, Now);

        clientCounter.Status.ShouldBe(OfferStatus.Rejected);
    }

    [Fact]
    public void RejectOffer_ByAuthor_Throws()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);

        Should.Throw<DomainRuleException>(() => announcement.RejectOffer(offer.Id, Helper, Now));

        offer.Status.ShouldBe(OfferStatus.Pending);
    }

    [Theory]
    [MemberData(nameof(NonParticipants))]
    public void RejectOffer_ByNonParticipant_Throws(UserId actorId)
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);
        announcement.AddHelperOffer(OtherHelper);

        Should.Throw<DomainRuleException>(() => announcement.RejectOffer(offer.Id, actorId, Now));

        offer.Status.ShouldBe(OfferStatus.Pending);
    }

    [Fact]
    public void RejectOffer_AlreadyRejected_Throws()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);
        announcement.RejectOffer(offer.Id, OwnerId, Now);

        Should.Throw<DomainRuleException>(() => announcement.RejectOffer(offer.Id, OwnerId, Now));
    }

    // ---------- WithdrawOffer ----------

    [Fact]
    public void WithdrawOffer_ByHelperOnOwnOffer_WithdrawsIt()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);

        announcement.WithdrawOffer(offer.Id, Helper, Now);

        offer.Status.ShouldBe(OfferStatus.Withdrawn);
        announcement.Status.ShouldBe(AnnouncementStatus.Open);
    }

    [Fact]
    public void WithdrawOffer_ByClientOnOwnCounter_WithdrawsIt()
    {
        var announcement = CreateAnnouncement();
        var first = announcement.AddHelperOffer(Helper);
        var clientCounter = announcement.Counter(first, OwnerId);

        announcement.WithdrawOffer(clientCounter.Id, OwnerId, Now);

        clientCounter.Status.ShouldBe(OfferStatus.Withdrawn);
    }

    [Fact]
    public void WithdrawOffer_ByOtherSide_Throws()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);

        Should.Throw<DomainRuleException>(() => announcement.WithdrawOffer(offer.Id, OwnerId, Now));

        offer.Status.ShouldBe(OfferStatus.Pending);
    }

    [Theory]
    [MemberData(nameof(NonParticipants))]
    public void WithdrawOffer_ByNonParticipant_Throws(UserId actorId)
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);
        announcement.AddHelperOffer(OtherHelper);

        Should.Throw<DomainRuleException>(() => announcement.WithdrawOffer(offer.Id, actorId, Now));

        offer.Status.ShouldBe(OfferStatus.Pending);
    }

    [Fact]
    public void WithdrawOffer_AfterAnnouncementAccepted_Throws()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);
        announcement.AcceptOffer(offer.Id, OwnerId, Now);

        Should.Throw<DomainRuleException>(() => announcement.WithdrawOffer(offer.Id, Helper, Now));

        offer.Status.ShouldBe(OfferStatus.Accepted);
    }

    // ---------- pełna negocjacja ----------

    [Fact]
    public void Negotiation_OfferCounterCounterAccept_ProducesChainOfOffers()
    {
        var announcement = CreateAnnouncement();

        var helperOffer = announcement.AddHelperOffer(Helper, price: 200m);
        var clientCounter = announcement.Counter(helperOffer, OwnerId, price: 120m);
        var helperCounter = announcement.Counter(clientCounter, Helper, price: 160m);
        announcement.AcceptOffer(helperCounter.Id, OwnerId, Now);

        announcement.Offers.Count.ShouldBe(3);
        helperOffer.Status.ShouldBe(OfferStatus.Countered);
        clientCounter.Status.ShouldBe(OfferStatus.Countered);
        helperCounter.Status.ShouldBe(OfferStatus.Accepted);

        clientCounter.ParentOfferId.ShouldBe(helperOffer.Id);
        helperCounter.ParentOfferId.ShouldBe(clientCounter.Id);

        announcement.AcceptedOfferId.ShouldBe(helperCounter.Id);
        announcement.Status.ShouldBe(AnnouncementStatus.Accepted);
    }

    // Stranger – nie bierze udziału w ogłoszeniu; OtherHelper – ma własną ofertę, ale to nie jego negocjacja.
    public static TheoryData<UserId> NonParticipants => new() { Stranger, OtherHelper };
}
