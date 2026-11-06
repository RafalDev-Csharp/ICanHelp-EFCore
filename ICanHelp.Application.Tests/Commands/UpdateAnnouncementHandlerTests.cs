using ICanHelp.Application.Commands;
using ICanHelp.Application.Commands.Handlers;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Tests.Fakes;
using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Tests.Commands;

/// <summary>Krok 9: PATCH ogłoszenia – podane pola się zmieniają, pominięte zostają, powstaje rewizja.</summary>
public class UpdateAnnouncementHandlerTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryAnnouncementRepository _announcements = new();
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FixedClock _clock = new();

    private readonly User _owner;
    private readonly User _stranger;
    private readonly Category _category;
    private readonly Guid _subcategoryId = Guid.NewGuid();
    private readonly Guid _otherSubcategoryId = Guid.NewGuid();
    private readonly Announcement _announcement;

    public UpdateAnnouncementHandlerTests()
    {
        _owner = AddUser("jan.kowalski", "jan@example.com");
        _stranger = AddUser("obcy", "obcy@example.com");

        _category = new Category(Id.New(), "Hydraulika");
        _category.AddSubcategory(new Id(_subcategoryId), "Krany");
        _category.AddSubcategory(new Id(_otherSubcategoryId), "Syfony");
        _categories.Categories.Add(_category);

        _announcement = new Announcement(Id.New(), _owner.Id, _category.Id, new Id(_subcategoryId),
            DifficultyLevel.Easy, "Cieknący kran", "Kran w kuchni cieknie od tygodnia.", Price.Pln(150),
            new Location(50.06, 19.94), _clock.UtcNow.AddDays(-1));
        _announcements.Announcements.Add(_announcement);
    }

    [Fact]
    public async Task Update_OnlyDescription_ChangesItAndKeepsOtherFields()
    {
        await Handler().HandleAsync(Command(_owner.Id) with { Description = "Kran cieknie, syfon też do wymiany." });

        _announcement.Description.Value.ShouldBe("Kran cieknie, syfon też do wymiany.");
        _announcement.Title.Value.ShouldBe("Cieknący kran");
        _announcement.Budget.ShouldBe(Price.Pln(150));
        _announcement.Revision.ShouldBe(1);
        _announcement.UpdatedAt.ShouldBe(_clock.UtcNow);
        _announcement.Changes.ShouldHaveSingleItem().ChangedFields.ShouldBe(AnnouncementFields.Description);
        _unitOfWork.Executions.ShouldBe(1);
    }

    [Fact]
    public async Task Update_BudgetAndLocation_RecordsBothInOneRevision()
    {
        await Handler().HandleAsync(Command(_owner.Id) with
        {
            Budget = new PriceDto { Amount = 200, Currency = "PLN" },
            Location = new LocationDto { Latitude = 50.07, Longitude = 19.95 }
        });

        _announcement.Budget.ShouldBe(Price.Pln(200));
        _announcement.Location.ShouldBe(new Location(50.07, 19.95));
        _announcement.Changes.ShouldHaveSingleItem().ChangedFields
            .ShouldBe(AnnouncementFields.Budget | AnnouncementFields.Location);
    }

    [Fact]
    public async Task Update_EmptyBody_DoesNotCreateRevision()
    {
        await Handler().HandleAsync(Command(_owner.Id));

        _announcement.Revision.ShouldBe(0);
        _announcement.Changes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Update_OtherSubcategoryOfSameCategory_ChangesCategory()
    {
        await Handler().HandleAsync(Command(_owner.Id) with { SubcategoryId = _otherSubcategoryId });

        _announcement.SubcategoryId.Value.ShouldBe(_otherSubcategoryId);
        _announcement.Changes.ShouldHaveSingleItem().ChangedFields.ShouldBe(AnnouncementFields.Category);
    }

    [Fact]
    public async Task Update_SubcategoryNotInCategory_Throws()
    {
        await Should.ThrowAsync<InvalidCategoryException>(() =>
            Handler().HandleAsync(Command(_owner.Id) with { SubcategoryId = Guid.NewGuid() }));

        _announcement.Revision.ShouldBe(0);
    }

    [Fact]
    public async Task Update_ByNotOwner_ThrowsDomainRule()
    {
        await Should.ThrowAsync<DomainRuleException>(() =>
            Handler().HandleAsync(Command(_stranger.Id) with { Title = "Przejęte ogłoszenie" }));

        _announcement.Title.Value.ShouldBe("Cieknący kran");
    }

    [Fact]
    public async Task Update_UnknownAnnouncement_Throws()
    {
        await Should.ThrowAsync<AnnouncementNotFoundException>(() =>
            Handler().HandleAsync(Command(_owner.Id) with { AnnouncementId = Guid.NewGuid(), Title = "Nowy" }));
    }

    [Fact]
    public async Task Update_LockedOwner_Throws()
    {
        _owner.Lock();

        await Should.ThrowAsync<UserLockedException>(() =>
            Handler().HandleAsync(Command(_owner.Id) with { Title = "Nowy tytuł" }));
    }

    private UpdateAnnouncementHandler Handler() => new(_announcements, _categories, _users, _unitOfWork, _clock);

    private UpdateAnnouncement Command(Guid actorId) => new() { AnnouncementId = _announcement.Id, ActorId = actorId };

    private User AddUser(string userName, string email)
    {
        var user = new User(UserId.New(), userName, "Test User", FakePasswordManager.Prefix + "Haslo123!",
            new ContactData(email, "+48600100200", "Polska", "Kraków", "30-001"), _clock.UtcNow);
        _users.Users.Add(user);
        return user;
    }
}
