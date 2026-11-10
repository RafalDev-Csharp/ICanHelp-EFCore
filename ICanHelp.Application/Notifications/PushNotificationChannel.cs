using ICanHelp.Application.DTO;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Notifications;

/// <summary>
/// Krok 15: push to all devices of the recipient (<c>api/me/devices</c>). No devices – nothing to do.
/// Tokens that FCM rejected as invalid (app removed, token expired) → those devices are removed.
/// Quiet hours stay where they were: an alert in quiet hours is not delivered live at all, so not by push either.
/// </summary>
internal sealed class PushNotificationChannel : INotificationChannel
{
    private readonly IUserDeviceRepository _devices;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPushSender _push;

    public PushNotificationChannel(IUserDeviceRepository devices, IUnitOfWork unitOfWork, IPushSender push)
    {
        _devices = devices;
        _unitOfWork = unitOfWork;
        _push = push;
    }

    public async Task SendAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default)
    {
        var recipient = new UserId(userId);
        var tokens = (await _devices.GetByUserAsync(recipient, cancellationToken)).Select(d => d.Token).ToList();
        if (tokens.Count == 0)
        {
            return;
        }

        var result = await _push.SendAsync(tokens, ToMessage(notification), cancellationToken);
        if (result.InvalidTokens.Count == 0)
        {
            return;
        }

        var invalid = result.InvalidTokens.ToHashSet(StringComparer.Ordinal);

        await _unitOfWork.ExecuteAsync(async () =>
        {
            foreach (var device in await _devices.GetByUserAsync(recipient, cancellationToken))
            {
                if (invalid.Contains(device.Token))
                {
                    _devices.Remove(device);
                }
            }
        }, cancellationToken);
    }

    public static PushMessage ToMessage(NotificationDto notification)
    {
        var data = new Dictionary<string, string>
        {
            ["notificationId"] = notification.Id.ToString(),
            ["type"] = notification.Type
        };

        if (notification.AnnouncementId is { } announcementId)
        {
            data["announcementId"] = announcementId.ToString();
        }

        if (notification.ConversationId is { } conversationId)
        {
            data["conversationId"] = conversationId.ToString();
        }

        if (notification.Url is { } url)
        {
            data["url"] = url;
        }

        return new PushMessage(notification.Title, notification.Body, data);
    }
}
