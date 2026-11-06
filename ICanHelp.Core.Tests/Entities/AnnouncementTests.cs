using ICanHelp.Core.Entities;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;
using static ICanHelp.Core.Tests.Entities.AnnouncementTestData;

namespace ICanHelp.Core.Tests.Entities;

/// <summary>Tworzenie, edycja, zdjęcia i cykl życia ogłoszenia (bez negocjacji).</summary>
public class AnnouncementTests
{
    // ---------- tworzenie ----------

    [Fact]
    public void Constructor_ValidData_CreatesOpenAnnouncementWithoutOffers()
    {
        var announcement = CreateAnnouncement();

        announcement.Status.ShouldBe(AnnouncementStatus.Open);
        announcement.OwnerId.ShouldBe(OwnerId);
        announcement.CreatedAt.ShouldBe(Now);
        announcement.Budget.ShouldBe(Price.Pln(150));
        announcement.AcceptedOfferId.ShouldBeNull();
        announcement.Offers.ShouldBeEmpty();
        announcement.Images.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_ZeroBudget_Throws()
    {
        Should.Throw<DomainRuleException>(() => CreateAnnouncement(Price.Pln(0)));
    }

    // ---------- edycja ----------

    [Fact]
    public void Update_WhenOpen_ChangesData()
    {
        var announcement = CreateAnnouncement();
        var categoryId = Id.New();
        var subcategoryId = Id.New();

        announcement.Update(OwnerId, categoryId, subcategoryId, DifficultyLevel.Hard, "Malowanie ściany",
            "Pomalować ścianę w salonie.", Price.Pln(300), new Location(50.0647, 19.9450), Now.AddHours(1));

        announcement.CategoryId.ShouldBe(categoryId);
        announcement.SubcategoryId.ShouldBe(subcategoryId);
        announcement.Difficulty.ShouldBe(DifficultyLevel.Hard);
        announcement.Title.Value.ShouldBe("Malowanie ściany");
        announcement.Description.Value.ShouldBe("Pomalować ścianę w salonie.");
        announcement.Budget.ShouldBe(Price.Pln(300));
        announcement.Location.ShouldBe(new Location(50.0647, 19.9450));
    }

    [Fact]
    public void Update_ZeroBudget_ThrowsAndKeepsOldBudget()
    {
        var announcement = CreateAnnouncement();

        Should.Throw<DomainRuleException>(() => announcement.Update(OwnerId, announcement.CategoryId,
            announcement.SubcategoryId, announcement.Difficulty, announcement.Title, announcement.Description,
            Price.Pln(0), announcement.Location, Now.AddHours(1)));

        announcement.Budget.ShouldBe(Price.Pln(150));
    }

    [Theory]
    [InlineData(AnnouncementStatus.Accepted)]
    [InlineData(AnnouncementStatus.InProgress)]
    [InlineData(AnnouncementStatus.Completed)]
    [InlineData(AnnouncementStatus.Cancelled)]
    public void Update_WhenNotOpen_Throws(AnnouncementStatus status)
    {
        var announcement = InStatus(status);

        Should.Throw<DomainRuleException>(() => announcement.Update(OwnerId, announcement.CategoryId,
            announcement.SubcategoryId, announcement.Difficulty, "Nowy tytuł", announcement.Description,
            announcement.Budget, announcement.Location, Now.AddHours(1)));
    }

    // ---------- zdjęcia ----------

    [Fact]
    public void AddImage_AddsImageLinkedToAnnouncement()
    {
        var announcement = CreateAnnouncement();
        var imageId = Id.New();

        var image = announcement.AddImage(imageId, "kran", ".jpg", "images/kran.jpg");

        image.Id.ShouldBe(imageId);
        image.AnnouncementId.ShouldBe(announcement.Id);
        announcement.Images.ShouldHaveSingleItem().ShouldBeSameAs(image);
    }

    [Fact]
    public void AddImage_MoreThanMax_Throws()
    {
        var announcement = CreateAnnouncement();
        for (var i = 0; i < Announcement.MaxImages; i++)
        {
            announcement.AddImage(Id.New(), $"img{i}", ".jpg", $"images/img{i}.jpg");
        }

        Should.Throw<DomainRuleException>(() => announcement.AddImage(Id.New(), "extra", ".jpg", "images/extra.jpg"));

        announcement.Images.Count.ShouldBe(Announcement.MaxImages);
    }

    [Fact]
    public void RemoveImage_ExistingImage_RemovesIt()
    {
        var announcement = CreateAnnouncement();
        var image = announcement.AddImage(Id.New(), "kran", ".jpg", "images/kran.jpg");

        announcement.RemoveImage(image.Id);

        announcement.Images.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveImage_UnknownImage_Throws()
    {
        var announcement = CreateAnnouncement();

        Should.Throw<DomainRuleException>(() => announcement.RemoveImage(Id.New()));
    }

    // ---------- cykl życia ----------

    [Fact]
    public void Start_WhenAccepted_ChangesStatusToInProgress()
    {
        var announcement = InStatus(AnnouncementStatus.Accepted);

        announcement.Start(OwnerId);

        announcement.Status.ShouldBe(AnnouncementStatus.InProgress);
    }

    [Theory]
    [InlineData(AnnouncementStatus.Open)]
    [InlineData(AnnouncementStatus.InProgress)]
    [InlineData(AnnouncementStatus.Completed)]
    [InlineData(AnnouncementStatus.Cancelled)]
    public void Start_WhenNotAccepted_Throws(AnnouncementStatus status)
    {
        var announcement = InStatus(status);

        Should.Throw<DomainRuleException>(() => announcement.Start(OwnerId));

        announcement.Status.ShouldBe(status);
    }

    [Fact]
    public void Complete_WhenInProgress_ChangesStatusToCompleted()
    {
        var announcement = InStatus(AnnouncementStatus.InProgress);

        announcement.Complete(OwnerId);

        announcement.Status.ShouldBe(AnnouncementStatus.Completed);
    }

    [Theory]
    [InlineData(AnnouncementStatus.Open)]
    [InlineData(AnnouncementStatus.Accepted)]
    [InlineData(AnnouncementStatus.Completed)]
    [InlineData(AnnouncementStatus.Cancelled)]
    public void Complete_WhenNotInProgress_Throws(AnnouncementStatus status)
    {
        var announcement = InStatus(status);

        Should.Throw<DomainRuleException>(() => announcement.Complete(OwnerId));

        announcement.Status.ShouldBe(status);
    }

    [Theory]
    [InlineData(AnnouncementStatus.Open)]
    [InlineData(AnnouncementStatus.Accepted)]
    public void Cancel_WhenOpenOrAccepted_ChangesStatusToCancelled(AnnouncementStatus status)
    {
        var announcement = InStatus(status);

        announcement.Cancel(OwnerId);

        announcement.Status.ShouldBe(AnnouncementStatus.Cancelled);
    }

    [Theory]
    [InlineData(AnnouncementStatus.InProgress)]
    [InlineData(AnnouncementStatus.Completed)]
    [InlineData(AnnouncementStatus.Cancelled)]
    public void Cancel_WhenInProgressCompletedOrCancelled_Throws(AnnouncementStatus status)
    {
        var announcement = InStatus(status);

        Should.Throw<DomainRuleException>(() => announcement.Cancel(OwnerId));

        announcement.Status.ShouldBe(status);
    }

    [Fact]
    public void FullLifecycle_OpenToCompleted()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);

        announcement.AcceptOffer(offer.Id, OwnerId);
        announcement.Status.ShouldBe(AnnouncementStatus.Accepted);

        announcement.Start(Helper);
        announcement.Status.ShouldBe(AnnouncementStatus.InProgress);

        announcement.Complete(OwnerId);
        announcement.Status.ShouldBe(AnnouncementStatus.Completed);
        announcement.AcceptedOfferId.ShouldBe(offer.Id);
    }
}
