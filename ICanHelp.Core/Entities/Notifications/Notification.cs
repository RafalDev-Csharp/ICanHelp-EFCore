using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities.Notifications;

/// <summary>
/// Aggregate root: one notification for one user (the list in the app, the unread counter). It is data – how it is
/// delivered (SignalR, e-mail, push) is a separate thing. Created from a domain event; (<see cref="UserId"/>,
/// <see cref="SourceEventId"/>) is unique, so handling the same event twice gives one notification.
/// </summary>
public class Notification
{
    public const int MaxPayloadLength = 4000;

    public Id Id { get; private set; }

    /// <summary>The recipient.</summary>
    public UserId UserId { get; private set; }

    public NotificationType Type { get; private set; }

    /// <summary>Parameters for the text (JSON object of strings, e.g. the announcement title, the price).</summary>
    public string Payload { get; private set; }

    public Id? AnnouncementId { get; private set; }
    public Id? ConversationId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    /// <summary>When the recipient read it (null = unread).</summary>
    public DateTime? ReadAt { get; private set; }

    /// <summary>The domain event (outbox message) this notification comes from.</summary>
    public Guid SourceEventId { get; private set; }

    // Required by EF Core.
    private Notification()
    {
    }

    public Notification(Id id, UserId userId, NotificationType type, string payload, Id? announcementId,
        Id? conversationId, Guid sourceEventId, DateTime createdAtUtc)
    {
        if (!Enum.IsDefined(type))
        {
            throw new DomainRuleException($"Notification type: {type} is invalid.");
        }

        if (string.IsNullOrWhiteSpace(payload) || payload.Length > MaxPayloadLength)
        {
            throw new DomainRuleException($"Notification payload must have 1–{MaxPayloadLength} characters.");
        }

        if (sourceEventId == Guid.Empty)
        {
            throw new DomainRuleException("Notification must come from an event.");
        }

        Id = id;
        UserId = userId;
        Type = type;
        Payload = payload;
        AnnouncementId = announcementId;
        ConversationId = conversationId;
        SourceEventId = sourceEventId;
        CreatedAt = createdAtUtc;
    }

    /// <summary>Only the recipient can mark it as read; the first read time stays.</summary>
    public void MarkAsRead(UserId userId, DateTime readAtUtc)
    {
        if (userId != UserId)
        {
            throw new DomainRuleException("Only the recipient can mark the notification as read.");
        }

        ReadAt ??= readAtUtc;
    }
}
