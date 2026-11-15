using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetLikedAnnouncementsHandler(ICanHelpDbContext db)
    : IQueryHandler<GetLikedAnnouncements, IReadOnlyList<LikedAnnouncementDto>>
{
    public async Task<IReadOnlyList<LikedAnnouncementDto>> HandleAsync(GetLikedAnnouncements query,
        CancellationToken cancellationToken = default)
    {
        var id = new UserId(query.UserId);

        var likes = await db.LikedAnnouncements
            .AsNoTracking()
            .Where(l => l.UserId == id)
            .OrderByDescending(l => l.LikedAt)
            .ToListAsync(cancellationToken);

        return likes.Select(l => l.AsDto()).ToList();
    }
}
