using ICanHelp.Core.Entities.Alerts;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Tests.Entities;

/// <summary>Krok 14: walidacja alertu, limit, dopasowanie kategorii i harmonogram (geometria jest w bazie).</summary>
public class AnnouncementAlertTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Location Augustow = new(53.84, 22.98);
    private static readonly Location Suwalki = new(54.10, 22.93);

    private static readonly UserId Owner = UserId.New();
    private static readonly Id Plumbing = Id.New();
    private static readonly Id LeakingTap = Id.New();
    private static readonly Id BlockedDrain = Id.New();

    private static AnnouncementAlert Area(double radiusKm = 5, params Id[] subcategories)
        => new(Id.New(), Owner, "Dom", AlertKind.Area, Plumbing, subcategories, [Augustow], radiusKm, Now);

    private static AnnouncementAlert Route(params Location[] points)
        => new(Id.New(), Owner, "Suwałki → Augustów", AlertKind.Route, Plumbing, [], points, 5, Now);

    [Fact]
    public void NewArea_IsEnabled_EveryDay_WithCentre()
    {
        var alert = Area();

        alert.IsEnabled.ShouldBeTrue();
        alert.ActiveDays.ShouldBe(WeekDays.All);
        alert.ActiveFrom.ShouldBeNull();
        alert.Center.ShouldBe(Augustow);
        alert.RoutePoints.ShouldBeEmpty();
        alert.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void NewRoute_KeepsPointsInOrder_HasNoCentre()
    {
        var alert = Route(Suwalki, Augustow);

        alert.Center.ShouldBeNull();
        alert.RoutePoints.ShouldBe(new[] { Suwalki, Augustow });
    }

    [Theory]
    [InlineData(0.4)]
    [InlineData(50.1)]
    [InlineData(double.NaN)]
    public void RadiusOutOfRange_Throws(double radiusKm)
        => Should.Throw<InvalidAlertException>(() => Area(radiusKm));

    [Theory]
    [InlineData(0.5)]
    [InlineData(50)]
    public void RadiusOnTheLimits_IsAccepted(double radiusKm)
        => Area(radiusKm).RadiusKm.ShouldBe(radiusKm);

    [Fact]
    public void Area_WithTwoPoints_Throws()
        => Should.Throw<InvalidAlertException>(() =>
            new AnnouncementAlert(Id.New(), Owner, "Dom", AlertKind.Area, Plumbing, [], [Augustow, Suwalki], 5, Now));

    [Fact]
    public void Route_WithOnePoint_Throws()
        => Should.Throw<InvalidAlertException>(() => Route(Suwalki));

    [Fact]
    public void Route_WithTheSamePointTwiceInARow_Throws()
        => Should.Throw<InvalidAlertException>(() => Route(Suwalki, Suwalki, Augustow));

    [Fact]
    public void Route_WithTooManyPoints_Throws()
    {
        var points = Enumerable.Range(0, AnnouncementAlert.MaxRoutePoints + 1)
            .Select(i => new Location(53 + i * 0.0001, 22.9))
            .ToArray();

        Should.Throw<InvalidAlertException>(() => Route(points));
    }

    [Fact]
    public void DuplicateSubcategories_AreStoredOnce()
        => Area(5, LeakingTap, LeakingTap).SubcategoryIds.ShouldBe(new[] { LeakingTap });

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    public void Limit_TenAlertsPerUser(int alreadyOwned, bool throws)
    {
        var create = () => AnnouncementAlert.EnsureCanCreate(alreadyOwned);

        if (throws)
        {
            Should.Throw<DomainRuleException>(create);
        }
        else
        {
            create();
        }
    }

    [Fact]
    public void Matches_WholeCategory_WhenNoSubcategories()
    {
        var alert = Area();

        alert.Matches(Plumbing, LeakingTap).ShouldBeTrue();
        alert.Matches(Plumbing, BlockedDrain).ShouldBeTrue();
        alert.Matches(Id.New(), LeakingTap).ShouldBeFalse();
    }

    [Fact]
    public void Matches_OnlyChosenSubcategories()
    {
        var alert = Area(5, LeakingTap);

        alert.Matches(Plumbing, LeakingTap).ShouldBeTrue();
        alert.Matches(Plumbing, BlockedDrain).ShouldBeFalse();
    }

    [Fact]
    public void Disabled_MatchesNothing_UntilEnabledAgain()
    {
        var alert = Area();

        alert.Disable(Owner, Now.AddHours(1));
        alert.IsEnabled.ShouldBeFalse();
        alert.UpdatedAt.ShouldBe(Now.AddHours(1));
        alert.Matches(Plumbing, LeakingTap).ShouldBeFalse();

        alert.Enable(Owner, Now.AddHours(2));
        alert.Matches(Plumbing, LeakingTap).ShouldBeTrue();
    }

    [Fact]
    public void OnlyOwner_CanChangeTheAlert()
    {
        var alert = Area();
        var stranger = UserId.New();

        Should.Throw<DomainRuleException>(() => alert.Disable(stranger, Now));
        Should.Throw<DomainRuleException>(() =>
            alert.Update(stranger, "Inny", Plumbing, [], [Suwalki], 10, Now));
        Should.Throw<DomainRuleException>(() => alert.SetSchedule(stranger, WeekDays.Weekend, null, null, Now));
    }

    [Fact]
    public void Update_ChangesValues_ButNotTheKind()
    {
        var alert = Area();

        alert.Update(Owner, "Praca", Plumbing, [BlockedDrain], [Suwalki], 10, Now.AddDays(1));

        alert.Name.Value.ShouldBe("Praca");
        alert.Center.ShouldBe(Suwalki);
        alert.RadiusKm.ShouldBe(10);
        alert.SubcategoryIds.ShouldBe(new[] { BlockedDrain });
        alert.UpdatedAt.ShouldBe(Now.AddDays(1));

        // Obszar dalej ma dokładnie jeden punkt – dwa to już trasa, a rodzaju się nie zmienia.
        Should.Throw<InvalidAlertException>(() =>
            alert.Update(Owner, "Praca", Plumbing, [], [Suwalki, Augustow], 10, Now));
    }

    [Fact]
    public void Schedule_WorkdaysAfternoon()
    {
        var alert = Area();
        alert.SetSchedule(Owner, WeekDays.Workdays, new TimeOnly(15, 0), new TimeOnly(18, 0), Now);

        // 12.01.2026 to poniedziałek, 17.01.2026 – sobota.
        alert.IsActiveAt(new DateTime(2026, 1, 12, 16, 30, 0)).ShouldBeTrue();
        alert.IsActiveAt(new DateTime(2026, 1, 12, 18, 0, 0)).ShouldBeFalse();
        alert.IsActiveAt(new DateTime(2026, 1, 12, 14, 59, 0)).ShouldBeFalse();
        alert.IsActiveAt(new DateTime(2026, 1, 17, 16, 30, 0)).ShouldBeFalse();
    }

    [Fact]
    public void Schedule_OverMidnight()
    {
        var alert = Area();
        alert.SetSchedule(Owner, WeekDays.All, new TimeOnly(22, 0), new TimeOnly(6, 0), Now);

        alert.IsActiveAt(new DateTime(2026, 1, 12, 23, 0, 0)).ShouldBeTrue();
        alert.IsActiveAt(new DateTime(2026, 1, 12, 5, 59, 0)).ShouldBeTrue();
        alert.IsActiveAt(new DateTime(2026, 1, 12, 12, 0, 0)).ShouldBeFalse();
    }

    [Fact]
    public void Schedule_WithoutHours_IsAllDay()
    {
        var alert = Area();
        alert.SetSchedule(Owner, WeekDays.Weekend, null, null, Now);

        alert.IsActiveAt(new DateTime(2026, 1, 17, 3, 0, 0)).ShouldBeTrue();
        alert.IsActiveAt(new DateTime(2026, 1, 12, 3, 0, 0)).ShouldBeFalse();
    }

    [Fact]
    public void Schedule_Invalid_Throws()
    {
        var alert = Area();

        Should.Throw<InvalidAlertException>(() => alert.SetSchedule(Owner, WeekDays.None, null, null, Now));
        Should.Throw<InvalidAlertException>(() => alert.SetSchedule(Owner, WeekDays.All, new TimeOnly(8, 0), null, Now));
        Should.Throw<InvalidAlertException>(() =>
            alert.SetSchedule(Owner, WeekDays.All, new TimeOnly(8, 0), new TimeOnly(8, 0), Now));
    }
}
