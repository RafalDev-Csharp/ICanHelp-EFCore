using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetConversationHandler : IQueryHandler<GetConversation, ConversationDto?>
{
    private readonly ICanHelpDbContext _db;

    public GetConversationHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<ConversationDto?> HandleAsync(GetConversation query, CancellationToken cancellationToken = default)
    {
        var id = new Id(query.ConversationId);
        var userId = new UserId(query.UserId);

        var conversation = await _db.Conversations
            .AsNoTracking()
            .Include(c => c.Messages)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

        // Obcy dostaje to samo co przy braku wątku.
        if (conversation is null || !conversation.IsParticipant(userId))
        {
            return null;
        }

        // Oferty i historia zmian są potrzebne do osi czasu i do IsOutdated/ChangesSinceOffer.
        var announcement = await _db.Announcements
            .AsNoTracking()
            .Include(a => a.Offers)
            .Include(a => a.Changes)
            .AsSplitQuery()
            .SingleOrDefaultAsync(a => a.Id == conversation.AnnouncementId, cancellationToken);

        return announcement is null ? null : conversation.AsDto(announcement, userId);
    }
}

internal sealed class GetConversationIdHandler : IQueryHandler<GetConversationId, Guid?>
{
    private readonly ICanHelpDbContext _db;

    public GetConversationIdHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<Guid?> HandleAsync(GetConversationId query, CancellationToken cancellationToken = default)
    {
        var announcementId = new Id(query.AnnouncementId);
        var helperId = new UserId(query.HelperId);

        var conversation = await _db.Conversations
            .AsNoTracking()
            .Where(c => c.AnnouncementId == announcementId && c.HelperId == helperId)
            .Select(c => c.Id)
            .SingleOrDefaultAsync(cancellationToken);

        return conversation?.Value;
    }
}
