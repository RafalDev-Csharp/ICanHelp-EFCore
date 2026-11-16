using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetConversationHandler(ICanHelpDbContext db) : IQueryHandler<GetConversation, ConversationDto?>
{
    public async Task<ConversationDto?> HandleAsync(GetConversation query, CancellationToken cancellationToken = default)
    {
        var id = new Id(query.ConversationId);
        var userId = new UserId(query.UserId);

        var conversation = await db.Conversations
            .AsNoTracking()
            .Include(c => c.Messages)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

        // Obcy dostaje to samo co przy braku wątku.
        if (conversation is null || !conversation.IsParticipant(userId))
        {
            return null;
        }

        // Oferty i historia zmian są potrzebne do osi czasu i do IsOutdated/ChangesSinceOffer.
        var announcement = await db.Announcements
            .AsNoTracking()
            .Include(a => a.Offers)
            .Include(a => a.Changes)
            .Include(a => a.JobExits)
            .AsSplitQuery()
            .SingleOrDefaultAsync(a => a.Id == conversation.AnnouncementId, cancellationToken);

        return announcement is null ? null : conversation.AsDto(announcement, userId);
    }
}

internal sealed class GetConversationIdHandler(ICanHelpDbContext db) : IQueryHandler<GetConversationId, Guid?>
{
    public async Task<Guid?> HandleAsync(GetConversationId query, CancellationToken cancellationToken = default)
    {
        var announcementId = new Id(query.AnnouncementId);
        var helperId = new UserId(query.HelperId);

        var conversation = await db.Conversations
            .AsNoTracking()
            .Where(c => c.AnnouncementId == announcementId && c.HelperId == helperId)
            .Select(c => c.Id)
            .SingleOrDefaultAsync(cancellationToken);

        return conversation?.Value;
    }
}
