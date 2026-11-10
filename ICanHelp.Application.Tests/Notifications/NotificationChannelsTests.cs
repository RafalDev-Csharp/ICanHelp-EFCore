using ICanHelp.Application.DTO;
using ICanHelp.Application.Notifications;
using ICanHelp.Application.Tests.Fakes;
using ICanHelp.Core.Entities.Notifications;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Enums;
using ICanHelp.Core.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace ICanHelp.Application.Tests.Notifications;

/// <summary>Krok 15: wszystkie kanały naraz (awaria jednego nie blokuje innych) i push na urządzenia odbiorcy.</summary>
public class NotificationChannelsTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUserDeviceRepository _devices = new();
    private readonly RecordingPushSender _push = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UserId _recipient = UserId.New();

    private static NotificationDto Dto(UserId recipient, Guid? announcementId = null, Guid? conversationId = null)
        => new Notification(Id.New(), recipient, NotificationType.OfferReceived,
                NotificationPayload.Serialize(new Dictionary<string, string>
                {
                    [NotificationPayload.AnnouncementTitle] = "Cieknący kran",
                    [NotificationPayload.Price] = "150.00 PLN"
                }),
                announcementId is { } a ? new Id(a) : null, conversationId is { } c ? new Id(c) : null,
                Guid.NewGuid(), Now)
            .ToDto();

    private UserDevice AddDevice(UserId user, string token)
    {
        var device = new UserDevice(Id.New(), user, token, DevicePlatform.Web, Now);
        _devices.Devices.Add(device);
        return device;
    }

    private PushNotificationChannel PushChannel() => new(_devices, _unitOfWork, _push);

    [Fact]
    public void Dto_HasTitleBodyAndUrl()
    {
        var conversationId = Guid.NewGuid();

        var dto = Dto(_recipient, Guid.NewGuid(), conversationId);

        dto.Title.ShouldBe("Nowa oferta: 150 zł");
        dto.Body.ShouldBe("Cieknący kran");
        dto.Url.ShouldBe($"/conversations/{conversationId}");
    }

    [Fact]
    public async Task Composite_FailingChannel_DoesNotStopTheOthers()
    {
        var failing = new RecordingChannel { Fail = true };
        var working = new RecordingChannel();
        var sender = new CompositeNotificationSender([failing, working],
            NullLogger<CompositeNotificationSender>.Instance);
        var dto = Dto(_recipient);

        await sender.SendAsync(_recipient, dto);

        working.Sent.ShouldHaveSingleItem().Notification.ShouldBe(dto);
    }

    [Fact]
    public async Task Composite_Cancellation_IsNotSwallowed()
    {
        var sender = new CompositeNotificationSender([new CancelledChannel()],
            NullLogger<CompositeNotificationSender>.Instance);

        await Should.ThrowAsync<OperationCanceledException>(() => sender.SendAsync(_recipient, Dto(_recipient)));
    }

    [Fact]
    public async Task Push_GoesToAllDevicesOfRecipientOnly()
    {
        AddDevice(_recipient, "phone");
        AddDevice(_recipient, "laptop");
        AddDevice(UserId.New(), "someone-else");
        var announcementId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var dto = Dto(_recipient, announcementId, conversationId);

        await PushChannel().SendAsync(_recipient, dto);

        var (tokens, message) = _push.Sent.ShouldHaveSingleItem();
        tokens.ShouldBe(new[] { "phone", "laptop" }, ignoreOrder: true);
        message.Title.ShouldBe(dto.Title);
        message.Body.ShouldBe(dto.Body);
        message.Data["notificationId"].ShouldBe(dto.Id.ToString());
        message.Data["type"].ShouldBe("OfferReceived");
        message.Data["announcementId"].ShouldBe(announcementId.ToString());
        message.Data["conversationId"].ShouldBe(conversationId.ToString());
        message.Data["url"].ShouldBe($"/conversations/{conversationId}");
        _unitOfWork.Executions.ShouldBe(0);
    }

    [Fact]
    public async Task Push_WithoutDevices_SendsNothing()
    {
        await PushChannel().SendAsync(_recipient, Dto(_recipient));

        _push.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Push_InvalidToken_RemovesThatDevice()
    {
        var valid = AddDevice(_recipient, "valid");
        AddDevice(_recipient, "expired");
        _push.InvalidTokens.Add("expired");

        await PushChannel().SendAsync(_recipient, Dto(_recipient));

        _devices.Devices.ShouldHaveSingleItem().ShouldBe(valid);
        _unitOfWork.Executions.ShouldBe(1);
    }

    [Fact]
    public void Message_WithoutIds_HasNoLinks()
    {
        var message = PushNotificationChannel.ToMessage(Dto(_recipient));

        message.Data.ContainsKey("announcementId").ShouldBeFalse();
        message.Data.ContainsKey("conversationId").ShouldBeFalse();
        message.Data.ContainsKey("url").ShouldBeFalse();
    }

    private sealed class CancelledChannel : INotificationChannel
    {
        public Task SendAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default)
            => throw new OperationCanceledException();
    }
}
