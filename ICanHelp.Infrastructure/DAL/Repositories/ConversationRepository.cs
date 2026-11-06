using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Repositories;

internal sealed class ConversationRepository : IConversationRepository
{
    private readonly ICanHelpDbContext _db;

    public ConversationRepository(ICanHelpDbContext db)
    {
        _db = db;
    }

    // The whole aggregate (with messages). Fine for short negotiations; paging of messages can come later.
    public Task<Conversation?> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
        => _db.Conversations
            .Include(c => c.Messages)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Conversation?> GetByParticipantsAsync(Id announcementId, UserId helperId,
        CancellationToken cancellationToken = default)
        => _db.Conversations
            .Include(c => c.Messages)
            .SingleOrDefaultAsync(c => c.AnnouncementId == announcementId && c.HelperId == helperId, cancellationToken);

    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
        => await _db.Conversations.AddAsync(conversation, cancellationToken);
}
