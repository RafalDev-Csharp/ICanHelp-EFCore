using System.Text.Json;
using System.Text.Json.Serialization;
using ICanHelp.Api.Tests.Infrastructure;
using ICanHelp.Application.DTO;
using ICanHelp.Core.Enums;

namespace ICanHelp.Api.Tests;

/// <summary>
/// Krok 15: <c>api/me/devices</c> (rejestracja idempotentna, przejęcie tokenu, usuwanie, cudze → 404) i push:
/// powiadomienie idzie na urządzenia odbiorcy (fake <see cref="RecordingPushSender"/> zamiast FCM), nigdy do autora.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public sealed class DeviceTests
{
    // DeviceDto.Platform – API zwraca enum jako tekst.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;

    public DeviceTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static string NewToken() => "fcm-" + Guid.NewGuid().ToString("N");

    private static async Task<DeviceDto> RegisterAsync(TestUser user, string token, string platform = "Web")
    {
        var response = await user.Client.PostAsJsonAsync("/api/me/devices", new { token, platform });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<DeviceDto>(Json)).ShouldNotBeNull();
    }

    private static async Task<List<DeviceDto>> GetDevicesAsync(TestUser user)
        => (await user.Client.GetFromJsonAsync<List<DeviceDto>>("/api/me/devices", Json))!;

    [Fact]
    public async Task Register_IsIdempotent_ListAndRemove()
    {
        var user = await _factory.CreateUserAsync();
        var token = NewToken();

        var first = await RegisterAsync(user, token);
        var again = await RegisterAsync(user, token, "Android");

        again.Id.ShouldBe(first.Id);
        again.Platform.ShouldBe(DevicePlatform.Android);
        again.LastSeenAt.ShouldBeGreaterThanOrEqualTo(first.LastSeenAt);
        (await GetDevicesAsync(user)).ShouldHaveSingleItem().Id.ShouldBe(first.Id);

        (await user.Client.DeleteAsync($"/api/me/devices/{first.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetDevicesAsync(user)).ShouldBeEmpty();
    }

    [Fact]
    public async Task SomeoneElsesDevice_IsNotFound_AndNotListed()
    {
        var owner = await _factory.CreateUserAsync();
        var stranger = await _factory.CreateUserAsync();
        var device = await RegisterAsync(owner, NewToken());

        await (await stranger.Client.DeleteAsync($"/api/me/devices/{device.Id}"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "DeviceNotFound");
        (await GetDevicesAsync(stranger)).ShouldBeEmpty();
        (await GetDevicesAsync(owner)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SameTokenFromAnotherUser_MovesTheDevice()
    {
        var first = await _factory.CreateUserAsync();
        var second = await _factory.CreateUserAsync();
        var token = NewToken();
        await RegisterAsync(first, token);

        await RegisterAsync(second, token);

        (await GetDevicesAsync(first)).ShouldBeEmpty();
        (await GetDevicesAsync(second)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Register_WithoutToken_IsBadRequest()
    {
        var user = await _factory.CreateUserAsync();

        await (await user.Client.PostAsJsonAsync("/api/me/devices", new { token = " ", platform = "Web" }))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "InvalidDevice");
    }

    [Fact]
    public async Task Devices_RequireSignIn()
        => (await _factory.CreateClient().GetAsync("/api/me/devices")).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Offer_PushesToOwnersDevices_NotToTheAuthor()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var ownerToken = NewToken();
        var helperToken = NewToken();
        await RegisterAsync(owner, ownerToken);
        await RegisterAsync(helper, helperToken);
        var announcementId = await owner.CreateAnnouncementAsync(title: "Wymiana zamka");

        await helper.MakeOfferAsync(announcementId, 120m);
        await _factory.ProcessOutboxAsync();

        var notification = (await owner.GetNotificationsAsync()).ShouldHaveSingleItem();
        notification.Title.ShouldBe("Nowa oferta: 120 zł");
        notification.Body.ShouldBe("Wymiana zamka");
        notification.Url.ShouldBe($"/conversations/{notification.ConversationId}");

        var push = _factory.Push.SentTo(ownerToken).ShouldHaveSingleItem();
        push.Title.ShouldBe(notification.Title);
        push.Body.ShouldBe(notification.Body);
        push.Data["notificationId"].ShouldBe(notification.Id.ToString());
        push.Data["announcementId"].ShouldBe(announcementId.ToString());
        push.Data["url"].ShouldBe(notification.Url);
        _factory.Push.SentTo(helperToken).ShouldBeEmpty();
    }

    [Fact]
    public async Task InvalidToken_RemovesTheDevice()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var expired = NewToken();
        var valid = NewToken();
        await RegisterAsync(owner, expired);
        var kept = await RegisterAsync(owner, valid);
        _factory.Push.InvalidTokens[expired] = true;
        var announcementId = await owner.CreateAnnouncementAsync();

        await helper.MakeOfferAsync(announcementId, 90m);
        await _factory.ProcessOutboxAsync();

        _factory.Push.SentTo(valid).ShouldHaveSingleItem();
        (await GetDevicesAsync(owner)).ShouldHaveSingleItem().Id.ShouldBe(kept.Id);
    }
}
