using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Tests.Entities;

/// <summary>Krok 15: urządzenie z push – walidacja tokenu, ponowne zgłoszenie, limit urządzeń.</summary>
public class UserDeviceTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private static UserDevice Create(UserId? userId = null, DateTime? at = null, string? token = null)
        => new(Id.New(), userId ?? UserId.New(), token ?? "token-" + Guid.NewGuid().ToString("N"),
            DevicePlatform.Web, at ?? Now);

    [Fact]
    public void Create_SetsLastSeenToCreation()
    {
        var device = Create();

        device.Platform.ShouldBe(DevicePlatform.Web);
        device.CreatedAt.ShouldBe(Now);
        device.LastSeenAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("two parts")]
    public void Create_WithWrongToken_Throws(string token)
        => Should.Throw<InvalidDeviceException>(() => Create(token: token));

    [Fact]
    public void Create_WithTooLongToken_Throws()
        => Should.Throw<InvalidDeviceException>(() => Create(token: new string('a', UserDevice.MaxTokenLength + 1)));

    [Fact]
    public void Create_WithUnknownPlatform_Throws()
        => Should.Throw<InvalidDeviceException>(() =>
            new UserDevice(Id.New(), UserId.New(), "token", (DevicePlatform)99, Now));

    [Fact]
    public void Refresh_BySameUser_MovesLastSeenForward()
    {
        var device = Create();

        device.Refresh(device.UserId, DevicePlatform.Android, Now.AddDays(1));

        device.LastSeenAt.ShouldBe(Now.AddDays(1));
        device.Platform.ShouldBe(DevicePlatform.Android);
        device.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Refresh_WithOlderTime_KeepsLastSeen()
    {
        var device = Create();

        device.Refresh(device.UserId, DevicePlatform.Web, Now.AddMinutes(-5));

        device.LastSeenAt.ShouldBe(Now);
    }

    [Fact]
    public void Refresh_ByAnotherUser_MovesTheDeviceToThatUser()
    {
        var device = Create();
        var other = UserId.New();

        device.Refresh(other, DevicePlatform.Web, Now.AddHours(1));

        device.UserId.ShouldBe(other);
        device.IsOwnedBy(other).ShouldBeTrue();
    }

    [Fact]
    public void SelectToRemove_UnderLimit_RemovesNothing()
    {
        var user = UserId.New();
        var devices = Enumerable.Range(0, UserDevice.MaxPerUser - 1).Select(i => Create(user, Now.AddHours(i))).ToList();

        UserDevice.SelectToRemoveBeforeAdding(devices).ShouldBeEmpty();
    }

    [Fact]
    public void SelectToRemove_AtLimit_RemovesTheLeastRecentlySeen()
    {
        var user = UserId.New();
        var devices = Enumerable.Range(0, UserDevice.MaxPerUser).Select(i => Create(user, Now.AddHours(i))).ToList();
        devices[0].Refresh(user, DevicePlatform.Web, Now.AddDays(1)); // seen again – the newest now

        var toRemove = UserDevice.SelectToRemoveBeforeAdding(devices).ShouldHaveSingleItem();

        toRemove.ShouldBe(devices[1]);
    }

    [Fact]
    public void SelectToRemove_OverLimit_RemovesEnoughForOneMore()
    {
        var user = UserId.New();
        var devices = Enumerable.Range(0, UserDevice.MaxPerUser + 2).Select(i => Create(user, Now.AddHours(i))).ToList();

        UserDevice.SelectToRemoveBeforeAdding(devices).ShouldBe(devices.Take(3));
    }
}
