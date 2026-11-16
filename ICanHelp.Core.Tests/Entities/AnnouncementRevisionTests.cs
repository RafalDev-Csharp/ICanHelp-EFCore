using ICanHelp.Core.Entities;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;
using static ICanHelp.Core.Tests.Entities.AnnouncementTestData;

namespace ICanHelp.Core.Tests.Entities;

/// <summary>Krok 9: edycja ogłoszenia tworzy rewizje, oferty pamiętają rewizję, nieaktualnej oferty nie da się przyjąć.</summary>
public class AnnouncementRevisionTests
{
    private static AnnouncementFields ChangeDescription(Announcement announcement, string description,
        UserId? actor = null, int hoursLater = 1)
        => announcement.Update(actor ?? OwnerId, announcement.CategoryId, announcement.SubcategoryId,
            announcement.Difficulty, announcement.Title, description, announcement.Budget, announcement.Location,
            Now.AddHours(hoursLater));

    // ---------- rewizje ----------

    [Fact]
    public void NewAnnouncement_HasRevisionZeroAndNoChanges()
    {
        var announcement = CreateAnnouncement();

        announcement.Revision.ShouldBe(0);
        announcement.UpdatedAt.ShouldBeNull();
        announcement.Changes.ShouldBeEmpty();
    }

    [Fact]
    public void Update_ChangedDescription_CreatesRevisionWithChangedFields()
    {
        var announcement = CreateAnnouncement();

        var changed = ChangeDescription(announcement, "Kran cieknie, a do tego trzeba wymienić syfon.");

        changed.ShouldBe(AnnouncementFields.Description);
        announcement.Revision.ShouldBe(1);
        announcement.UpdatedAt.ShouldBe(Now.AddHours(1));
        var change = announcement.Changes.ShouldHaveSingleItem();
        change.Revision.ShouldBe(1);
        change.AnnouncementId.ShouldBe(announcement.Id);
        change.ChangedAt.ShouldBe(Now.AddHours(1));
        change.ChangedFields.ShouldBe(AnnouncementFields.Description);
        var field = change.Fields.ShouldHaveSingleItem();
        field.Field.ShouldBe(AnnouncementFields.Description);
        field.OldValue.ShouldBe("Kran w kuchni cieknie od tygodnia.");
        field.NewValue.ShouldBe("Kran cieknie, a do tego trzeba wymienić syfon.");
    }

    [Fact]
    public void Update_OneLetterChanged_IsAChange()
    {
        var announcement = CreateAnnouncement();

        // „od tygodnia” → „od tygodni” – jedna litera, ale nadal zmiana, której pomocnik musi być świadomy.
        var changed = ChangeDescription(announcement, "Kran w kuchni cieknie od tygodni.");

        changed.ShouldBe(AnnouncementFields.Description);
        announcement.Revision.ShouldBe(1);
    }

    [Fact]
    public void Update_BudgetAndLocation_StoresValuesAsText()
    {
        var announcement = CreateAnnouncement();

        announcement.Update(OwnerId, announcement.CategoryId, announcement.SubcategoryId, announcement.Difficulty,
            announcement.Title, announcement.Description, Price.Pln(199.5m), new Location(50.0614, 19.9366),
            Now.AddHours(1));

        var fields = announcement.Changes.ShouldHaveSingleItem().Fields;
        var budget = fields.Single(f => f.Field == AnnouncementFields.Budget);
        budget.OldValue.ShouldBe("150.00 PLN");
        budget.NewValue.ShouldBe("199.50 PLN");
        var location = fields.Single(f => f.Field == AnnouncementFields.Location);
        location.OldValue.ShouldBe("52.2297, 21.0122");
        location.NewValue.ShouldBe("50.0614, 19.9366");
    }

    [Fact]
    public void Update_SeveralFields_AllAreRecordedInOneRevision()
    {
        var announcement = CreateAnnouncement();

        var changed = announcement.Update(OwnerId, announcement.CategoryId, announcement.SubcategoryId,
            DifficultyLevel.Hard, "Cieknący kran i syfon", announcement.Description, Price.Pln(250),
            announcement.Location, Now.AddHours(1));

        changed.ShouldBe(AnnouncementFields.Difficulty | AnnouncementFields.Title | AnnouncementFields.Budget);
        announcement.Revision.ShouldBe(1);
        announcement.Changes.ShouldHaveSingleItem().ChangedFields.ShouldBe(changed);
    }

    [Fact]
    public void Update_SameValues_DoesNotCreateRevision()
    {
        var announcement = CreateAnnouncement();

        var changed = ChangeDescription(announcement, announcement.Description);

        changed.ShouldBe(AnnouncementFields.None);
        announcement.Revision.ShouldBe(0);
        announcement.UpdatedAt.ShouldBeNull();
        announcement.Changes.ShouldBeEmpty();
    }

    [Fact]
    public void Update_ByNotOwner_ThrowsAndKeepsData()
    {
        var announcement = CreateAnnouncement();
        var description = announcement.Description;

        Should.Throw<DomainRuleException>(() => ChangeDescription(announcement, "Zmiana od obcego.", Helper));

        announcement.Description.ShouldBe(description);
        announcement.Revision.ShouldBe(0);
    }

    [Fact]
    public void GetChangesSince_ReturnsFieldsChangedAfterRevision()
    {
        var announcement = CreateAnnouncement();
        ChangeDescription(announcement, "Pierwsza poprawka opisu.");
        announcement.Update(OwnerId, announcement.CategoryId, announcement.SubcategoryId, announcement.Difficulty,
            announcement.Title, announcement.Description, Price.Pln(200), announcement.Location, Now.AddHours(2));

        announcement.GetChangesSince(0).ShouldBe(AnnouncementFields.Description | AnnouncementFields.Budget);
        announcement.GetChangesSince(1).ShouldBe(AnnouncementFields.Budget);
        announcement.GetChangesSince(2).ShouldBe(AnnouncementFields.None);
    }

    // ---------- różnice od chwili oferty („było → jest”) ----------

    private static void Edit(Announcement announcement, string description, decimal budget, int hoursLater)
        => announcement.Update(OwnerId, announcement.CategoryId, announcement.SubcategoryId, announcement.Difficulty,
            announcement.Title, description, Price.Pln(budget), announcement.Location, Now.AddHours(hoursLater));

    [Fact]
    public void LaterEdit_CannotHideEarlierPriceChange()
    {
        // Scenariusz z rozmowy: cena 150 → 500 razem z dopiskiem w opisie, a zaraz potem opis wraca do pierwotnego.
        var announcement = CreateAnnouncement();
        var original = announcement.Description.Value;
        var offer = announcement.AddHelperOffer(Helper);

        Edit(announcement, original + " Syfon też do wymiany.", 500m, hoursLater: 1);
        Edit(announcement, original, 500m, hoursLater: 2);

        // Cena nadal różni się od tej z chwili oferty – jest na liście; opis wrócił co do znaku – nie ma go.
        var diff = announcement.GetFieldChangesSince(offer.AnnouncementRevision).ShouldHaveSingleItem();
        diff.Field.ShouldBe(AnnouncementFields.Budget);
        diff.OldValue.ShouldBe("150.00 PLN");
        diff.NewValue.ShouldBe("500.00 PLN");
        announcement.GetEditsSince(offer.AnnouncementRevision).ShouldBe(2);
        announcement.Changes.Count.ShouldBe(2);
        Should.Throw<DomainRuleException>(() => announcement.AcceptOffer(offer.Id, OwnerId, Now));
    }

    [Fact]
    public void SeveralEditsOfOneField_AreMergedFromOfferTimeToNow()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);

        Edit(announcement, announcement.Description, 300m, hoursLater: 1);
        Edit(announcement, announcement.Description, 500m, hoursLater: 2);

        var diff = announcement.GetFieldChangesSince(offer.AnnouncementRevision).ShouldHaveSingleItem();
        diff.OldValue.ShouldBe("150.00 PLN");
        diff.NewValue.ShouldBe("500.00 PLN");
    }

    [Fact]
    public void EverythingRevertedToOfferTime_NoDifferences_ButEditsCountedAndOfferStillBlocked()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);

        Edit(announcement, announcement.Description, 500m, hoursLater: 1);
        Edit(announcement, announcement.Description, 150m, hoursLater: 2);

        announcement.GetFieldChangesSince(offer.AnnouncementRevision).ShouldBeEmpty();
        announcement.GetChangesSince(offer.AnnouncementRevision).ShouldBe(AnnouncementFields.None);
        announcement.GetEditsSince(offer.AnnouncementRevision).ShouldBe(2);
        // Blokada zostaje (decyzja z planu): potwierdzenie = kontroferta na bieżącej rewizji.
        Should.Throw<DomainRuleException>(() => announcement.AcceptOffer(offer.Id, OwnerId, Now));
    }

    [Fact]
    public void OfferMadeAfterEdits_SeesNoDifferences()
    {
        var announcement = CreateAnnouncement();
        Edit(announcement, announcement.Description, 500m, hoursLater: 1);

        var offer = announcement.AddHelperOffer(Helper);

        announcement.GetFieldChangesSince(offer.AnnouncementRevision).ShouldBeEmpty();
        announcement.GetEditsSince(offer.AnnouncementRevision).ShouldBe(0);
    }

    // ---------- oferty a rewizje ----------

    [Fact]
    public void Offers_RememberRevisionTheyWereMadeOn()
    {
        var announcement = CreateAnnouncement();
        var before = announcement.AddHelperOffer(Helper);

        ChangeDescription(announcement, "Doszedł jeszcze syfon.");
        var after = announcement.AddHelperOffer(OtherHelper);

        before.AnnouncementRevision.ShouldBe(0);
        after.AnnouncementRevision.ShouldBe(1);
    }

    [Fact]
    public void AcceptOffer_MadeBeforeEdit_ThrowsAndKeepsAnnouncementOpen()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);
        ChangeDescription(announcement, "Przy okazji pomalować ścianę.");

        Should.Throw<DomainRuleException>(() => announcement.AcceptOffer(offer.Id, OwnerId, Now));

        offer.Status.ShouldBe(OfferStatus.Pending);
        announcement.Status.ShouldBe(AnnouncementStatus.Open);
        announcement.AcceptedOfferId.ShouldBeNull();
    }

    [Fact]
    public void AcceptOffer_ClientCounterAfterEdit_HelperCanAccept()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper, price: 120m);
        ChangeDescription(announcement, "Przy okazji wymienić syfon.");

        // Kontroferta powstaje na bieżącej rewizji – to sposób na „odświeżenie” nieaktualnej oferty.
        var counter = announcement.Counter(offer, OwnerId, price: 160m);
        announcement.AcceptOffer(counter.Id, Helper, Now);

        counter.AnnouncementRevision.ShouldBe(1);
        counter.Status.ShouldBe(OfferStatus.Accepted);
        announcement.Status.ShouldBe(AnnouncementStatus.Accepted);
    }

    [Fact]
    public void AcceptOffer_EditWithoutRealChange_OfferStaysAcceptable()
    {
        var announcement = CreateAnnouncement();
        var offer = announcement.AddHelperOffer(Helper);
        ChangeDescription(announcement, announcement.Description);

        announcement.AcceptOffer(offer.Id, OwnerId, Now);

        offer.Status.ShouldBe(OfferStatus.Accepted);
    }
}
