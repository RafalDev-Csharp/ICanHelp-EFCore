using ICanHelp.Core.Entities;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;
using static ICanHelp.Core.Tests.Entities.AnnouncementTestData;

namespace ICanHelp.Core.Tests.Entities;

/// <summary>Krok 10: zdjęcia – tylko właściciel, tylko Open, limit 5, każda zmiana to nowa rewizja (flaga Images).</summary>
public class AnnouncementImageTests
{
    private static Image AddImage(Announcement announcement, UserId? actor = null, int minutesLater = 10,
        Id? imageId = null)
    {
        var id = imageId ?? Id.New();
        return announcement.AddImage(actor ?? OwnerId, id, "kran.jpg", ".jpg", $"{announcement.Id.Value:N}/{id.Value:N}.jpg",
            Now.AddMinutes(minutesLater));
    }

    // ---------- dodawanie ----------

    [Fact]
    public void AddImage_ByOwner_AddsImageLinkedToAnnouncement()
    {
        var announcement = CreateAnnouncement();
        var imageId = Id.New();

        var image = AddImage(announcement, imageId: imageId);

        image.Id.ShouldBe(imageId);
        image.AnnouncementId.ShouldBe(announcement.Id);
        image.FileName.Value.ShouldBe("kran.jpg");
        image.FileExtension.Value.ShouldBe(".jpg");
        image.AddedAt.ShouldBe(Now.AddMinutes(10));
        announcement.Images.ShouldHaveSingleItem().ShouldBeSameAs(image);
    }

    [Fact]
    public void AddImage_CreatesRevisionWithImagesFlagAndIdsBeforeAfter()
    {
        var announcement = CreateAnnouncement();
        var first = AddImage(announcement, minutesLater: 10);

        var second = AddImage(announcement, minutesLater: 20);

        announcement.Revision.ShouldBe(2);
        announcement.UpdatedAt.ShouldBe(Now.AddMinutes(20));
        var change = announcement.Changes.OrderBy(c => c.Revision).Last();
        change.Revision.ShouldBe(2);
        change.ChangedFields.ShouldBe(AnnouncementFields.Images);
        var field = change.Fields.ShouldHaveSingleItem();
        field.Field.ShouldBe(AnnouncementFields.Images);
        field.OldValue.ShouldBe($"{first.Id.Value}");
        field.NewValue.ShouldBe($"{first.Id.Value}, {second.Id.Value}");
    }

    [Fact]
    public void AddImage_ByNotOwner_Throws()
    {
        var announcement = CreateAnnouncement();

        Should.Throw<DomainRuleException>(() => AddImage(announcement, actor: Helper));

        announcement.Images.ShouldBeEmpty();
        announcement.Revision.ShouldBe(0);
    }

    [Theory]
    [InlineData(AnnouncementStatus.Accepted)]
    [InlineData(AnnouncementStatus.InProgress)]
    [InlineData(AnnouncementStatus.Completed)]
    [InlineData(AnnouncementStatus.Cancelled)]
    public void AddImage_NotOpen_Throws(AnnouncementStatus status)
    {
        var announcement = InStatus(status);

        Should.Throw<DomainRuleException>(() => AddImage(announcement));

        announcement.Images.ShouldBeEmpty();
    }

    [Fact]
    public void AddImage_MoreThanMax_Throws()
    {
        var announcement = CreateAnnouncement();
        for (var i = 0; i < Announcement.MaxImages; i++)
        {
            AddImage(announcement, minutesLater: i);
        }

        Should.Throw<DomainRuleException>(() => AddImage(announcement, minutesLater: 30));

        announcement.Images.Count.ShouldBe(Announcement.MaxImages);
        announcement.Revision.ShouldBe(Announcement.MaxImages);
    }

    [Fact]
    public void AddImage_SameIdTwice_Throws()
    {
        var announcement = CreateAnnouncement();
        var imageId = Id.New();
        AddImage(announcement, imageId: imageId);

        Should.Throw<DomainRuleException>(() => AddImage(announcement, imageId: imageId, minutesLater: 20));

        announcement.Images.Count.ShouldBe(1);
    }

    [Fact]
    public void AddImage_MakesPendingOfferOutdated_SoItCannotBeAccepted()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);

        var image = AddImage(announcement);

        announcement.GetChangesSince(offer.AnnouncementRevision).ShouldBe(AnnouncementFields.Images);
        var diff = announcement.GetFieldChangesSince(offer.AnnouncementRevision).ShouldHaveSingleItem();
        diff.OldValue.ShouldBe(string.Empty);
        diff.NewValue.ShouldBe(image.Id.Value.ToString());
        Should.Throw<DomainRuleException>(() => announcement.AcceptOffer(offer.Id, OwnerId));
    }

    // ---------- usuwanie ----------

    [Fact]
    public void RemoveImage_ByOwner_RemovesItAndCreatesRevision()
    {
        var announcement = CreateAnnouncement();
        var image = AddImage(announcement);

        var removed = announcement.RemoveImage(OwnerId, image.Id, Now.AddHours(1));

        removed.ShouldBeSameAs(image);
        announcement.Images.ShouldBeEmpty();
        announcement.Revision.ShouldBe(2);
        announcement.UpdatedAt.ShouldBe(Now.AddHours(1));
        var field = announcement.Changes.Single(c => c.Revision == 2).Fields.ShouldHaveSingleItem();
        field.Field.ShouldBe(AnnouncementFields.Images);
        field.OldValue.ShouldBe(image.Id.Value.ToString());
        field.NewValue.ShouldBe(string.Empty);
    }

    [Fact]
    public void RemoveImage_UnknownImage_ThrowsNotFound()
    {
        var announcement = CreateAnnouncement();

        Should.Throw<ImageNotFoundException>(() => announcement.RemoveImage(OwnerId, Id.New(), Now.AddHours(1)));

        announcement.Revision.ShouldBe(0);
    }

    [Fact]
    public void RemoveImage_ByNotOwner_Throws()
    {
        var announcement = CreateAnnouncement();
        var image = AddImage(announcement);

        Should.Throw<DomainRuleException>(() => announcement.RemoveImage(Stranger, image.Id, Now.AddHours(1)));

        announcement.Images.ShouldHaveSingleItem();
    }

    [Fact]
    public void RemoveImage_NotOpen_Throws()
    {
        var announcement = CreateAnnouncement();
        var image = AddImage(announcement);
        announcement.Cancel(OwnerId);

        Should.Throw<DomainRuleException>(() => announcement.RemoveImage(OwnerId, image.Id, Now.AddHours(1)));

        announcement.Images.ShouldHaveSingleItem();
    }

    [Fact]
    public void AddThenRemoveAfterOffer_NoNetDifferenceButEditsAreCounted()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);
        var image = AddImage(announcement);

        announcement.RemoveImage(OwnerId, image.Id, Now.AddHours(1));

        // Zdjęcia są takie jak przy ofercie – nie ma czego podświetlać, ale oferta i tak jest nieaktualna
        // (pomocnik widzi „edytowano 2 razy po Twojej ofercie”).
        announcement.GetFieldChangesSince(offer.AnnouncementRevision).ShouldBeEmpty();
        announcement.GetEditsSince(offer.AnnouncementRevision).ShouldBe(2);
        Should.Throw<DomainRuleException>(() => announcement.AcceptOffer(offer.Id, OwnerId));
    }

    // ---------- miniatura ----------

    [Fact]
    public void GetThumbnail_IsTheOldestImage()
    {
        var announcement = CreateAnnouncement();
        announcement.GetThumbnail().ShouldBeNull();

        var first = AddImage(announcement, minutesLater: 10);
        var second = AddImage(announcement, minutesLater: 20);
        announcement.GetThumbnail().ShouldBeSameAs(first);

        announcement.RemoveImage(OwnerId, first.Id, Now.AddMinutes(30));
        announcement.GetThumbnail().ShouldBeSameAs(second);
    }
}
