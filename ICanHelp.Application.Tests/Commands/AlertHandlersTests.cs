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

/// <summary>Krok 14: komendy alertów – właściciel z tokenu, kategoria, limit, PATCH, cudzy alert = 404.</summary>
public class AlertHandlersTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly InMemoryAnnouncementAlertRepository _alerts = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FixedClock _clock = new();

    private readonly User _user;
    private readonly Category _plumbing;
    private readonly Category _painting;
    private readonly Guid _leakingTap;

    private static readonly LocationDto Augustow = new() { Latitude = 53.84, Longitude = 22.98 };
    private static readonly LocationDto Suwalki = new() { Latitude = 54.10, Longitude = 22.93 };

    public AlertHandlersTests()
    {
        _user = new User(UserId.New(), "hydraulik", "Jan Hydraulik", FakePasswordManager.Prefix + "Haslo123!",
            new ContactData("hydraulik@example.com", "+48600100200", "Polska", "Augustów", "16-300"), _clock.UtcNow);
        _users.Users.Add(_user);

        _plumbing = new Category(Id.New(), "Hydraulika");
        _leakingTap = _plumbing.AddSubcategory(Id.New(), "Cieknący kran").Id;
        _painting = new Category(Id.New(), "Malowanie");
        _painting.AddSubcategory(Id.New(), "Ściany");
        _categories.Categories.AddRange([_plumbing, _painting]);
    }

    private CreateAlert AreaCommand() => new()
    {
        AlertId = Guid.NewGuid(),
        UserId = _user.Id,
        Name = "Dom",
        Kind = AlertKind.Area,
        CategoryId = _plumbing.Id,
        Center = Augustow,
        RadiusKm = 5
    };

    private CreateAlertHandler CreateHandler() => new(_alerts, _categories, _users, _unitOfWork, _clock);
    private UpdateAlertHandler UpdateHandler() => new(_alerts, _categories, _unitOfWork, _clock);

    [Fact]
    public async Task Create_Area_AddsEnabledAlertOfTheUser()
    {
        var command = AreaCommand() with
        {
            SubcategoryIds = [_leakingTap],
            Schedule = new AlertScheduleDto { Days = WeekDays.Workdays, From = new TimeOnly(15, 0), To = new TimeOnly(18, 0) }
        };

        await CreateHandler().HandleAsync(command);

        var alert = _alerts.Alerts.ShouldHaveSingleItem();
        alert.Id.Value.ShouldBe(command.AlertId);
        alert.UserId.ShouldBe(_user.Id);
        alert.Kind.ShouldBe(AlertKind.Area);
        alert.Center.ShouldBe(new Location(53.84, 22.98));
        alert.SubcategoryIds.ShouldBe(new[] { new Id(_leakingTap) });
        alert.ActiveDays.ShouldBe(WeekDays.Workdays);
        alert.ActiveFrom.ShouldBe(new TimeOnly(15, 0));
        alert.IsEnabled.ShouldBeTrue();
        alert.CreatedAt.ShouldBe(_clock.UtcNow);
        _unitOfWork.Executions.ShouldBe(1);
    }

    [Fact]
    public async Task Create_Route_UsesRoutePoints()
    {
        var command = AreaCommand() with { Kind = AlertKind.Route, Center = null, RoutePoints = [Suwalki, Augustow] };

        await CreateHandler().HandleAsync(command);

        _alerts.Alerts.ShouldHaveSingleItem().RoutePoints.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Create_AreaWithoutCentre_Throws()
        => await Should.ThrowAsync<InvalidAlertException>(() =>
            CreateHandler().HandleAsync(AreaCommand() with { Center = null }));

    [Fact]
    public async Task Create_SubcategoryOfOtherCategory_Throws()
    {
        var command = AreaCommand() with { SubcategoryIds = [_painting.Subcategories.First().Id.Value] };

        await Should.ThrowAsync<InvalidCategoryException>(() => CreateHandler().HandleAsync(command));
        _alerts.Alerts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_UnknownCategory_Throws()
        => await Should.ThrowAsync<InvalidCategoryException>(() =>
            CreateHandler().HandleAsync(AreaCommand() with { CategoryId = Guid.NewGuid() }));

    [Fact]
    public async Task Create_EleventhAlert_Throws()
    {
        for (var i = 0; i < 10; i++)
        {
            await CreateHandler().HandleAsync(AreaCommand());
        }

        await Should.ThrowAsync<DomainRuleException>(() => CreateHandler().HandleAsync(AreaCommand()));
        _alerts.Alerts.Count.ShouldBe(10);
    }

    [Fact]
    public async Task Update_OnlySentFields_ChangeCategoryClearsSubcategories()
    {
        var create = AreaCommand() with { SubcategoryIds = [_leakingTap] };
        await CreateHandler().HandleAsync(create);

        await UpdateHandler().HandleAsync(new UpdateAlert
        {
            AlertId = create.AlertId,
            UserId = _user.Id,
            CategoryId = _painting.Id,
            RadiusKm = 12
        });

        var alert = _alerts.Alerts.Single();
        alert.Name.Value.ShouldBe("Dom");
        alert.CategoryId.ShouldBe(_painting.Id);
        alert.SubcategoryIds.ShouldBeEmpty();
        alert.RadiusKm.ShouldBe(12);
        alert.Center.ShouldBe(new Location(53.84, 22.98));
    }

    [Fact]
    public async Task Update_RouteForAreaAlert_Throws()
    {
        var create = AreaCommand();
        await CreateHandler().HandleAsync(create);

        await Should.ThrowAsync<InvalidAlertException>(() => UpdateHandler().HandleAsync(new UpdateAlert
        {
            AlertId = create.AlertId,
            UserId = _user.Id,
            RoutePoints = [Suwalki, Augustow]
        }));
    }

    [Fact]
    public async Task SomebodyElsesAlert_IsNotFound()
    {
        var create = AreaCommand();
        await CreateHandler().HandleAsync(create);
        var stranger = Guid.NewGuid();

        await Should.ThrowAsync<AlertNotFoundException>(() => UpdateHandler().HandleAsync(
            new UpdateAlert { AlertId = create.AlertId, UserId = stranger, RadiusKm = 1 }));
        await Should.ThrowAsync<AlertNotFoundException>(() =>
            new DeleteAlertHandler(_alerts, _unitOfWork).HandleAsync(new DeleteAlert(create.AlertId, stranger)));
        await Should.ThrowAsync<AlertNotFoundException>(() =>
            new SetAlertEnabledHandler(_alerts, _unitOfWork, _clock)
                .HandleAsync(new SetAlertEnabled(create.AlertId, stranger, false)));

        _alerts.Alerts.ShouldHaveSingleItem().IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task DisableAndDelete_ByOwner()
    {
        var create = AreaCommand();
        await CreateHandler().HandleAsync(create);

        await new SetAlertEnabledHandler(_alerts, _unitOfWork, _clock)
            .HandleAsync(new SetAlertEnabled(create.AlertId, _user.Id, false));
        _alerts.Alerts.Single().IsEnabled.ShouldBeFalse();

        await new DeleteAlertHandler(_alerts, _unitOfWork).HandleAsync(new DeleteAlert(create.AlertId, _user.Id));
        _alerts.Alerts.ShouldBeEmpty();
    }
}
