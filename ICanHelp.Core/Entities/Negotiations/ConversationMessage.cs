using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities.Negotiations;

/// <summary>
/// One message in a <see cref="Conversation"/> (text up to 500 characters). Messages are not offers – they cannot be accepted.
/// Created only through <see cref="Conversation.SendMessage"/>.
/// </summary>
public class ConversationMessage
{
    public Id Id { get; private set; }
    public Id ConversationId { get; private set; }
    public UserId SenderId { get; private set; }
    public Description Text { get; private set; }
    public DateTime SentAt { get; private set; }

    /// <summary>
    /// <see cref="Announcement.Revision"/> when the message was written – the UI can mark a question that refers
    /// to an older version of the announcement.
    /// </summary>
    public int AnnouncementRevision { get; private set; }

    // Required by EF Core.
    private ConversationMessage()
    {
    }

    internal ConversationMessage(Id id, Id conversationId, UserId senderId, Description text, DateTime sentAtUtc,
        int announcementRevision)
    {
        Id = id;
        ConversationId = conversationId;
        SenderId = senderId;
        Text = text;
        SentAt = sentAtUtc;
        AnnouncementRevision = announcementRevision;
    }
}
