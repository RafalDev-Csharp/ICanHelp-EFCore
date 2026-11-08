namespace ICanHelp.Core.Events;

/// <summary>A message in a conversation; the recipient is the other participant than <paramref name="SenderId"/>.</summary>
public sealed record MessageSent(
    Guid ConversationId,
    Guid AnnouncementId,
    Guid MessageId,
    Guid ClientId,
    Guid HelperId,
    Guid SenderId,
    string Text,
    DateTime OccurredAt) : IDomainEvent;
