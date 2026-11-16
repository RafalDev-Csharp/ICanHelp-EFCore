using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Repositories;

public interface IConversationRepository
{
    /// <summary>Loads the conversation together with its messages.</summary>
    Task<Conversation?> GetByIdAsync(Id id, CancellationToken cancellationToken = default);

    /// <summary>The conversation of this helper about this announcement (there is at most one).</summary>
    Task<Conversation?> GetByParticipantsAsync(Id announcementId, UserId helperId,
        CancellationToken cancellationToken = default);

    /// <summary>Conversations about the announcement – only their ids and helpers (e.g. whom to notify about an edit).</summary>
    Task<IReadOnlyList<(Id ConversationId, UserId HelperId)>> GetParticipantsAsync(Id announcementId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);
}
