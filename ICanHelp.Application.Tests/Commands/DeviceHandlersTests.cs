using ICanHelp.Application.Commands;
using ICanHelp.Application.Commands.Handlers;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Tests.Fakes;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Enums;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Tests.Commands;

/// <summary>Krok 15: rejestracja urządzenia (idempotentna, przejęcie tokenu, limit) i usuwanie.</summary>
public class DeviceHandlersTests
{
    private readonly InMemoryUserDeviceRepository _devices = new();
    private readonly InMemoryUserRepository _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly MutableClock _clock = new();
    private readonly User _user;
    private readonly User _other;

    public DeviceHandlersTests()
    {
        _user = CreateUser("jan");
        _other = CreateUser("anna");
    }

    private User CreateUser(string userName)
    {
        var user = new User(UserId.New(), userName, "Test User", FakePasswordManager.Prefix + "Haslo123!",
            new ContactData($"{userName}@example.com", "+48600100200", "Polska", "Augustów", "16-300"), _clock.UtcNow);
        _users.Users.Add(user);
        return user;
    }

    private Task RegisterAsync(User user, string token, DevicePlatform platform = DevicePlatform.Web, Guid? id = null)
        => new RegisterDeviceHandler(_devices, _users, _unitOfWork, _clock).HandleAsync(new RegisterDevice
        {
            DeviceId = id ?? Guid.NewGuid(),
            UserId = user.Id,
            Token = token,
            Platform = platform
        });

    [Fact]
    public async Task Register_NewToken_AddsDevice()
    {
        var id = Guid.NewGuid();

        await RegisterAsync(_user, "token-1", id: id);

        var device = _devices.Devices.ShouldHaveSingleItem();
        device.Id.Value.ShouldBe(id);
        device.UserId.ShouldBe(_user.Id);
        device.Token.ShouldBe("token-1");
        device.LastSeenAt.ShouldBe(_clock.UtcNow);
    }

    [Fact]
    public async Task Register_SameTokenAgain_RefreshesTheSameDevice()
    {
        await RegisterAsync(_user, "token-1");
        var firstId = _devices.Devices.Single().Id;
        _clock.UtcNow = _clock.UtcNow.AddDays(2);

        await RegisterAsync(_user, "token-1", DevicePlatform.Android);

        var device = _devices.Devices.ShouldHaveSingleItem();
        device.Id.ShouldBe(firstId);
        device.LastSeenAt.ShouldBe(_clock.UtcNow);
        device.Platform.ShouldBe(DevicePlatform.Android);
    }

    [Fact]
    public async Task Register_TokenOfAnotherUser_MovesDeviceToMe()
    {
        await RegisterAsync(_other, "shared-laptop");

        await RegisterAsync(_user, "shared-laptop");

        _devices.Devices.ShouldHaveSingleItem().UserId.ShouldBe(_user.Id);
    }

    [Fact]
    public async Task Register_OverLimit_RemovesLeastRecentlySeen()
    {
        for (var i = 0; i < UserDevice.MaxPerUser; i++)
        {
            await RegisterAsync(_user, $"token-{i}");
            _clock.UtcNow = _clock.UtcNow.AddHours(1);
        }

        await RegisterAsync(_user, "token-new");

        _devices.Devices.Count.ShouldBe(UserDevice.MaxPerUser);
        _devices.Devices.ShouldNotContain(d => d.Token == "token-0");
        _devices.Devices.ShouldContain(d => d.Token == "token-new");
    }

    [Fact]
    public async Task Register_LockedUser_Throws()
    {
        _user.Lock();

        await Should.ThrowAsync<UserLockedException>(() => RegisterAsync(_user, "token-1"));
        _devices.Devices.ShouldBeEmpty();
    }

    [Fact]
    public async Task Remove_OwnDevice_RemovesIt()
    {
        await RegisterAsync(_user, "token-1");
        var device = _devices.Devices.Single();

        await new RemoveDeviceHandler(_devices, _unitOfWork).HandleAsync(new RemoveDevice(device.Id, _user.Id));

        _devices.Devices.ShouldBeEmpty();
    }

    [Fact]
    public async Task Remove_SomeoneElsesDevice_NotFound()
    {
        await RegisterAsync(_other, "token-1");
        var device = _devices.Devices.Single();

        await Should.ThrowAsync<DeviceNotFoundException>(() =>
            new RemoveDeviceHandler(_devices, _unitOfWork).HandleAsync(new RemoveDevice(device.Id, _user.Id)));
        _devices.Devices.ShouldHaveSingleItem();
    }
}
