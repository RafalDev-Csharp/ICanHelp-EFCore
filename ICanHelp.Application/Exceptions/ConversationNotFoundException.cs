using ICanHelp.Core.Exceptions;

namespace ICanHelp.Application.Exceptions;

public class ConversationNotFoundException : NotFoundException
{
    public Guid ConversationId { get; }

    public ConversationNotFoundException(Guid conversationId)
        : base($"Conversation with id: {conversationId} was not found.")
    {
        ConversationId = conversationId;
    }
}
