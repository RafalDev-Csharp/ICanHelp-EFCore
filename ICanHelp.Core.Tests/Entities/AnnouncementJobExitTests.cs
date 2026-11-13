using ICanHelp.Core.Enums;
using ICanHelp.Core.Events;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;
using static ICanHelp.Core.Tests.Entities.AnnouncementTestData;

namespace ICanHelp.Core.Tests.Entities;

/// <summary>F11 E: rezygnacja przed rozpoczęciem (Accepted → Open) i przerwanie w trakcie (InProgress → Interrupted).</summary>
public class AnnouncementJobExitTests
{
    // ---------- rezygnacja ----------

    [Fact]
    public void Abandon_ByHelper_ReopensAnnouncementAndMarksOfferAbandoned()
    {
        var announcement = InStatus(AnnouncementStatus.Accepted);
        var offerId = announcement.AcceptedOfferId!;
        announcement.ClearDomainEvents();

        var exit = announcement.Abandon(Helper, JobExitReason.CannotMakeIt, null, Now.AddHours(5));

        announcement.Status.ShouldBe(AnnouncementStatus.Open);
        announcement.AcceptedOfferId.ShouldBeNull();
        announcement.Offers.Single(o => o.Id == offerId).Status.ShouldBe(OfferStatus.Abandoned);

        exit.Kind.ShouldBe(JobExitKind.Abandoned);
        exit.Number.ShouldBe(1);
        exit.OfferId.ShouldBe(offerId);
        exit.HelperId.ShouldBe(Helper);
        exit.ActorId.ShouldBe(Helper);
        exit.ActorParty.ShouldBe(OfferParty.Helper);
        exit.Reason.ShouldBe(JobExitReason.CannotMakeIt);
        exit.OccurredAt.ShouldBe(Now.AddHours(5));
        announcement.JobExits.ShouldHaveSingleItem().ShouldBe(exit);

        var e = announcement.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<JobAbandoned>();
        e.OwnerId.ShouldBe(OwnerId.Value);
        e.HelperId.ShouldBe(Helper.Value);
        e.ActorId.ShouldBe(Helper.Value);
        e.ActorParty.ShouldBe(OfferParty.Helper);
        e.Reason.ShouldBe(JobExitReason.CannotMakeIt);
    }

    [Fact]
    public void Abandon_ByOwner_WithNote_RecordsClientSide()
    {
        var announcement = InStatus(AnnouncementStatus.Accepted);

        var exit = announcement.Abandon(OwnerId, JobExitReason.Other, new Description("Sam naprawiłem."), Now);

        exit.ActorParty.ShouldBe(OfferParty.Client);
        exit.HelperId.ShouldBe(Helper);
        exit.Note!.Value.ShouldBe("Sam naprawiłem.");
        announcement.DomainEvents.OfType<JobAbandoned>().Single().Note.ShouldBe("Sam naprawiłem.");
    }

    [Fact]
    public void Abandon_ByStranger_Throws()
    {
        var announcement = InStatus(AnnouncementStatus.Accepted);

        Should.Throw<DomainRuleException>(() => announcement.Abandon(Stranger, JobExitReason.NoContact, null, Now));

        announcement.Status.ShouldBe(AnnouncementStatus.Accepted);
        announcement.JobExits.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(AnnouncementStatus.Open)]
    [InlineData(AnnouncementStatus.InProgress)]
    [InlineData(AnnouncementStatus.Completed)]
    [InlineData(AnnouncementStatus.Cancelled)]
    [InlineData(AnnouncementStatus.Interrupted)]
    public void Abandon_WhenNotAccepted_Throws(AnnouncementStatus status)
    {
        var announcement = InStatus(status);

        Should.Throw<DomainRuleException>(() => announcement.Abandon(Helper, JobExitReason.NoContact, null, Now));
    }

    [Fact]
    public void Abandon_OtherWithoutNote_Throws()
    {
        var announcement = InStatus(AnnouncementStatus.Accepted);

        Should.Throw<InvalidJobExitException>(() => announcement.Abandon(Helper, JobExitReason.Other, null, Now));

        announcement.Status.ShouldBe(AnnouncementStatus.Accepted);
    }

    [Fact]
    public void Abandon_WithDisagreement_Throws()
    {
        var announcement = InStatus(AnnouncementStatus.Accepted);

        Should.Throw<InvalidJobExitException>(
            () => announcement.Abandon(Helper, JobExitReason.Disagreement, null, Now));
    }

    [Fact]
    public void Abandon_WithUnknownReason_Throws()
    {
        var announcement = InStatus(AnnouncementStatus.Accepted);

        Should.Throw<InvalidJobExitException>(() => announcement.Abandon(Helper, (JobExitReason)99, null, Now));
    }

    [Fact]
    public void AfterAbandon_TheHelperCannotOfferOrWriteButOthersCan()
    {
        var announcement = InStatus(AnnouncementStatus.Accepted);
        announcement.Abandon(OwnerId, JobExitReason.NoContact, null, Now);

        Should.Throw<DomainRuleException>(() => announcement.AddHelperOffer(Helper));
        Should.Throw<DomainRuleException>(() => announcement.StartConversation(Id.New(), Helper, Now));
        announcement.AllowsConversationWith(Helper).ShouldBeFalse();
        announcement.HasLeftJob(Helper).ShouldBeTrue();

        announcement.AllowsConversationWith(OtherHelper).ShouldBeTrue();
        var offer = announcement.AddHelperOffer(OtherHelper, 130m);
        announcement.AcceptOffer(offer.Id, OwnerId, Now.AddHours(1));
        announcement.Status.ShouldBe(AnnouncementStatus.Accepted);
        announcement.GetAcceptedHelperId().ShouldBe(OtherHelper);
    }

    [Fact]
    public void Abandon_SecondTime_GetsNextNumber()
    {
        var announcement = InStatus(AnnouncementStatus.Accepted);
        announcement.Abandon(Helper, JobExitReason.CannotMakeIt, null, Now);
        var offer = announcement.AddHelperOffer(OtherHelper);
        announcement.AcceptOffer(offer.Id, OwnerId, Now.AddHours(1));

        var exit = announcement.Abandon(OwnerId, JobExitReason.TermsDiffer, null, Now.AddHours(2));

        exit.Number.ShouldBe(2);
        exit.HelperId.ShouldBe(OtherHelper);
        announcement.JobExits.Count.ShouldBe(2);
    }

    [Fact]
    public void Abandon_CannotBeRated()
    {
        var announcement = InStatus(AnnouncementStatus.Accepted);
        announcement.Abandon(Helper, JobExitReason.CannotMakeIt, null, Now);

        Should.Throw<DomainRuleException>(
            () => announcement.CreateRating(Id.New(), OwnerId, new Rate(1), null, Now));
    }

    // ---------- przerwanie ----------

    [Fact]
    public void Interrupt_ByOwner_EndsAsInterruptedAndRaisesEvent()
    {
        var announcement = InStatus(AnnouncementStatus.InProgress);
        var offerId = announcement.AcceptedOfferId!;
        announcement.ClearDomainEvents();

        var exit = announcement.Interrupt(OwnerId, JobExitReason.Disagreement, null, Now.AddDays(1));

        announcement.Status.ShouldBe(AnnouncementStatus.Interrupted);
        announcement.AcceptedOfferId.ShouldBe(offerId);
        announcement.Offers.Single(o => o.Id == offerId).Status.ShouldBe(OfferStatus.Accepted);
        announcement.GetInterruption().ShouldBe(exit);
        exit.Kind.ShouldBe(JobExitKind.Interrupted);
        exit.ActorParty.ShouldBe(OfferParty.Client);

        var e = announcement.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<JobInterrupted>();
        e.HelperId.ShouldBe(Helper.Value);
        e.ActorId.ShouldBe(OwnerId.Value);
        e.Reason.ShouldBe(JobExitReason.Disagreement);
    }

    [Fact]
    public void Interrupt_ConversationIsReadOnlyAndBothSidesCanRate()
    {
        var announcement = InStatus(AnnouncementStatus.Interrupted);

        announcement.AllowsConversationWith(Helper).ShouldBeFalse();

        var byOwner = announcement.CreateRating(Id.New(), OwnerId, new Rate(2), null, Now.AddDays(3));
        byOwner.RateeId.ShouldBe(Helper);
        var byHelper = announcement.CreateRating(Id.New(), Helper, new Rate(4), null, Now.AddDays(3));
        byHelper.RateeId.ShouldBe(OwnerId);

        Should.Throw<DomainRuleException>(
            () => announcement.CreateRating(Id.New(), Stranger, new Rate(1), null, Now.AddDays(3)));
    }

    [Theory]
    [InlineData(AnnouncementStatus.Open)]
    [InlineData(AnnouncementStatus.Accepted)]
    [InlineData(AnnouncementStatus.Completed)]
    [InlineData(AnnouncementStatus.Cancelled)]
    [InlineData(AnnouncementStatus.Interrupted)]
    public void Interrupt_WhenNotInProgress_Throws(AnnouncementStatus status)
    {
        var announcement = InStatus(status);

        Should.Throw<DomainRuleException>(() => announcement.Interrupt(OwnerId, JobExitReason.NoContact, null, Now));
    }

    [Fact]
    public void Interrupt_ByStranger_Throws()
    {
        var announcement = InStatus(AnnouncementStatus.InProgress);

        Should.Throw<DomainRuleException>(() => announcement.Interrupt(Stranger, JobExitReason.NoContact, null, Now));

        announcement.Status.ShouldBe(AnnouncementStatus.InProgress);
    }

    [Fact]
    public void Interrupt_OtherWithoutNote_Throws()
    {
        var announcement = InStatus(AnnouncementStatus.InProgress);

        Should.Throw<InvalidJobExitException>(() => announcement.Interrupt(Helper, JobExitReason.Other, null, Now));
    }

    [Fact]
    public void Interrupted_CannotBeCompletedOrCancelled()
    {
        var announcement = InStatus(AnnouncementStatus.Interrupted);

        Should.Throw<DomainRuleException>(() => announcement.Complete(OwnerId, Now));
        Should.Throw<DomainRuleException>(() => announcement.Cancel(OwnerId));
    }
}
