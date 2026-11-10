using ICanHelp.Application.DTO;
using ICanHelp.Core.Entities.Notifications;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Notifications;

/// <summary>What to notify about and whom – built by the event handlers, saved by <see cref="NotificationPublisher"/>.</summary>
internal sealed record NotificationDraft(
    UserId RecipientId,
    NotificationType Type,
    Id? AnnouncementId,
    Id? ConversationId,
    IReadOnlyDictionary<string, string> Parameters);

/// <summary>
/// Saves notifications for one event and then delivers them through the channels.
/// Rules for everybody: never to the author of the action, one per recipient and event, and an event handled again
/// (retry from the outbox) does not create a second one (<see cref="INotificationRepository.ExistsAsync"/> +
/// unique index UserId + SourceEventId). Delivery only after the commit and only of the new ones.
/// </summary>
internal sealed class NotificationPublisher
{
    private readonly INotificationRepository _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationSender _sender;

    public NotificationPublisher(INotificationRepository notifications, IUnitOfWork unitOfWork,
        INotificationSender sender)
    {
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _sender = sender;
    }

    public async Task PublishAsync(Guid eventId, UserId actorId, DateTime occurredAtUtc,
        IEnumerable<NotificationDraft> drafts, CancellationToken cancellationToken)
    {
        var toCreate = drafts
            .Where(d => d.RecipientId != actorId)
            .DistinctBy(d => d.RecipientId)
            .ToList();

        if (toCreate.Count == 0)
        {
            return;
        }

        var created = new List<Notification>();

        await _unitOfWork.ExecuteAsync(async () =>
        {
            created.Clear();

            foreach (var draft in toCreate)
            {
                if (await _notifications.ExistsAsync(draft.RecipientId, eventId, cancellationToken))
                {
                    continue;
                }

                var notification = new Notification(Id.New(), draft.RecipientId, draft.Type,
                    NotificationPayload.Serialize(draft.Parameters), draft.AnnouncementId, draft.ConversationId,
                    eventId, occurredAtUtc);

                await _notifications.AddAsync(notification, cancellationToken);
                created.Add(notification);
            }
        }, cancellationToken);

        foreach (var notification in created)
        {
            await _sender.SendAsync(notification.UserId, notification.ToDto(), cancellationToken);
        }
    }
}

public static class NotificationMapping
{
    public static NotificationDto ToDto(this Notification notification)
    {
        var parameters = NotificationPayload.Deserialize(notification.Payload);
        var text = NotificationTextFormatter.Format(notification.Type, parameters);
        var announcementId = notification.AnnouncementId?.Value;
        var conversationId = notification.ConversationId?.Value;

        return new NotificationDto
        {
            Id = notification.Id,
            Title = text.Title,
            Body = text.Body,
            Url = NotificationTextFormatter.Link(notification.Type, announcementId, conversationId),
            Type = notification.Type.ToString(),
            Parameters = parameters,
            AnnouncementId = announcementId,
            ConversationId = conversationId,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt
        };
    }
}
