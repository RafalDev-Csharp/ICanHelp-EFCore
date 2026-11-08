using ICanHelp.Core.Entities;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;
using static ICanHelp.Core.Tests.Entities.AnnouncementTestData;

namespace ICanHelp.Core.Tests.Entities;

/// <summary>Kto może wykonać krok cyklu życia (start, zakończenie, anulowanie) i wystawić ocenę.</summary>
public class AnnouncementParticipantsTests
{
    // ---------- kto może ----------

    [Fact]
    public void Start_ByAcceptedHelper_ChangesStatusToInProgress()
    {
        var announcement = InStatus(AnnouncementStatus.Accepted);

        announcement.Start(Helper);

        announcement.Status.ShouldBe(AnnouncementStatus.InProgress);
    }

    [Fact]
    public void Start_ByStranger_Throws()
    {
        var announcement = InStatus(AnnouncementStatus.Accepted);

        Should.Throw<DomainRuleException>(() => announcement.Start(Stranger));

        announcement.Status.ShouldBe(AnnouncementStatus.Accepted);
    }

    [Fact]
    public void Start_ByHelperWhoseOfferWasRejected_Throws()
    {
        var announcement = CreateAnnouncement();
        var accepted = announcement.AddHelperOffer(Helper);
        announcement.AddHelperOffer(OtherHelper);
        announcement.AcceptOffer(accepted.Id, OwnerId, Now);

        Should.Throw<DomainRuleException>(() => announcement.Start(OtherHelper));
    }

    [Fact]
    public void Complete_ByHelper_Throws()
    {
        var announcement = InStatus(AnnouncementStatus.InProgress);

        Should.Throw<DomainRuleException>(() => announcement.Complete(Helper));

        announcement.Status.ShouldBe(AnnouncementStatus.InProgress);
    }

    [Theory]
    [InlineData(AnnouncementStatus.Open)]
    [InlineData(AnnouncementStatus.Accepted)]
    public void Cancel_ByNotOwner_Throws(AnnouncementStatus status)
    {
        var announcement = InStatus(status);

        Should.Throw<DomainRuleException>(() => announcement.Cancel(Helper));

        announcement.Status.ShouldBe(status);
    }

    [Fact]
    public void GetAcceptedHelperId_BeforeAndAfterAcceptance()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);

        announcement.GetAcceptedHelperId().ShouldBeNull();
        announcement.IsParticipant(Helper).ShouldBeFalse();

        announcement.AcceptOffer(offer.Id, OwnerId, Now);

        announcement.GetAcceptedHelperId().ShouldBe(Helper);
        announcement.IsParticipant(Helper).ShouldBeTrue();
        announcement.IsParticipant(OwnerId).ShouldBeTrue();
        announcement.IsParticipant(Stranger).ShouldBeFalse();
    }

    [Fact]
    public void GetAcceptedHelperId_AfterCounterOfferAccepted_ReturnsHelperOfTheChain()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);
        var counter = announcement.Counter(offer, OwnerId);

        announcement.AcceptOffer(counter.Id, Helper, Now);

        announcement.GetAcceptedHelperId().ShouldBe(Helper);
    }

    // ---------- ocena ----------

    [Fact]
    public void CreateRating_ByOwner_RatesTheHelper()
    {
        var announcement = InStatus(AnnouncementStatus.Completed);

        var rating = announcement.CreateRating(Id.New(), OwnerId, new Rate(5), "Szybko i porządnie.", Now.AddDays(3));

        rating.AnnouncementId.ShouldBe(announcement.Id);
        rating.RaterId.ShouldBe(OwnerId);
        rating.RateeId.ShouldBe(Helper);
        rating.Rate.Value.ShouldBe(5);
        rating.Comment!.Value.ShouldBe("Szybko i porządnie.");
        rating.CreatedAt.ShouldBe(Now.AddDays(3));
    }

    [Fact]
    public void CreateRating_ByHelper_RatesTheOwner()
    {
        var announcement = InStatus(AnnouncementStatus.Completed);

        var rating = announcement.CreateRating(Id.New(), Helper, new Rate(4), null, Now.AddDays(3));

        rating.RaterId.ShouldBe(Helper);
        rating.RateeId.ShouldBe(OwnerId);
        rating.Comment.ShouldBeNull();
    }

    [Fact]
    public void CreateRating_ByStranger_Throws()
    {
        var announcement = InStatus(AnnouncementStatus.Completed);

        Should.Throw<DomainRuleException>(
            () => announcement.CreateRating(Id.New(), Stranger, new Rate(1), null, Now.AddDays(3)));
    }

    [Theory]
    [InlineData(AnnouncementStatus.Open)]
    [InlineData(AnnouncementStatus.Accepted)]
    [InlineData(AnnouncementStatus.InProgress)]
    [InlineData(AnnouncementStatus.Cancelled)]
    public void CreateRating_WhenNotCompleted_Throws(AnnouncementStatus status)
    {
        var announcement = InStatus(status);

        Should.Throw<DomainRuleException>(
            () => announcement.CreateRating(Id.New(), OwnerId, new Rate(5), null, Now.AddDays(3)));
    }

    // ---------- trudność ----------

    [Fact]
    public void Constructor_UndefinedDifficulty_Throws()
    {
        Should.Throw<DomainRuleException>(() => new Announcement(
            Id.New(), OwnerId, Id.New(), Id.New(), (DifficultyLevel)99,
            "Cieknący kran", "Kran w kuchni cieknie od tygodnia.", Price.Pln(150), new Location(52.2, 21.0), Now));
    }
}
