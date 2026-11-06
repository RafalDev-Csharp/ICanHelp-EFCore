using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities.Negotiations;

/// <summary>
/// Aggregate root: the conversation between the announcement owner (client) and one helper – questions and explanations
/// ("why more expensive?") next to the formal offers. A separate aggregate, so chatting neither loads nor locks
/// the announcement. One conversation per (announcement, helper) – enforced by the application layer and a unique index.
/// Created by <see cref="Announcement.StartConversation"/> (checks that the announcement is open).
/// </summary>
public class Conversation
{
    private readonly List<ConversationMessage> _messages = new();

    public Id Id { get; private set; }
    public Id AnnouncementId { get; private set; }

    /// <summary>The announcement owner.</summary>
    public UserId ClientId { get; private set; }
    public UserId HelperId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    /// <summary>Until when the client has read the conversation (null = never opened it).</summary>
    public DateTime? ClientLastReadAt { get; private set; }

    /// <summary>Until when the helper has read the conversation (null = never opened it).</summary>
    public DateTime? HelperLastReadAt { get; private set; }

    public IReadOnlyCollection<ConversationMessage> Messages => _messages.AsReadOnly();

    // Required by EF Core.
    private Conversation()
    {
    }

    public Conversation(Id id, Id announcementId, UserId clientId, UserId helperId, DateTime createdAtUtc)
    {
        if (clientId == helperId)
        {
            throw new DomainRuleException("The owner cannot talk to himself about his announcement.");
        }

        Id = id;
        AnnouncementId = announcementId;
        ClientId = clientId;
        HelperId = helperId;
        CreatedAt = createdAtUtc;
    }

    /// <summary>
    /// One of the two participants writes a message. Whether the conversation is still allowed depends on the announcement
    /// (<see cref="Announcement.AllowsConversationWith"/>): any helper while it is open, after acceptance only the accepted one.
    /// The message remembers the revision of the announcement it was written for. The sender has read everything up to now.
    /// </summary>
    public ConversationMessage SendMessage(Id messageId, UserId senderId, Description text, Announcement announcement,
        DateTime sentAtUtc)
    {
        if (announcement.Id != AnnouncementId)
        {
            throw new DomainRuleException("The announcement does not belong to this conversation.");
        }

        GetParty(senderId);

        if (!announcement.AllowsConversationWith(HelperId))
        {
            throw new DomainRuleException(
                $"Announcement in status {announcement.Status} does not allow messages in this conversation.");
        }

        if (_messages.Any(m => m.Id == messageId))
        {
            throw new DomainRuleException($"Message with id: {messageId} already exists.");
        }

        var message = new ConversationMessage(messageId, Id, senderId, text, sentAtUtc, announcement.Revision);
        _messages.Add(message);

        MarkAsRead(senderId, sentAtUtc);
        return message;
    }

    /// <summary>The participant has seen the conversation up to the given moment. The read mark never goes back.</summary>
    public void MarkAsRead(UserId userId, DateTime readAtUtc)
    {
        if (GetParty(userId) == OfferParty.Client)
        {
            ClientLastReadAt = Later(ClientLastReadAt, readAtUtc);
        }
        else
        {
            HelperLastReadAt = Later(HelperLastReadAt, readAtUtc);
        }
    }

    public bool IsParticipant(UserId userId) => userId == ClientId || userId == HelperId;

    /// <summary>On which side the user is (Client = owner, Helper); not a participant → <see cref="DomainRuleException"/>.</summary>
    public OfferParty GetParty(UserId userId)
    {
        if (userId == ClientId)
        {
            return OfferParty.Client;
        }

        if (userId == HelperId)
        {
            return OfferParty.Helper;
        }

        throw new DomainRuleException("The user is not a participant of this conversation.");
    }

    /// <summary>Until when the participant has read the conversation (null = never).</summary>
    public DateTime? GetLastReadAt(UserId userId)
        => GetParty(userId) == OfferParty.Client ? ClientLastReadAt : HelperLastReadAt;

    /// <summary>Messages of the other side written after the participant last read the conversation.</summary>
    public int CountUnread(UserId userId)
    {
        var lastReadAt = GetLastReadAt(userId);
        return _messages.Count(m => m.SenderId != userId && (lastReadAt is null || m.SentAt > lastReadAt));
    }

    private static DateTime Later(DateTime? current, DateTime candidate)
        => current is { } value && value > candidate ? value : candidate;
}
