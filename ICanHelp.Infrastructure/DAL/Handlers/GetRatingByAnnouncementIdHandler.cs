using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetRatingByAnnouncementIdHandler(ICanHelpDbContext db)
    : IQueryHandler<GetRatingByAnnouncementId, IReadOnlyList<RatingDto>>
{
    public async Task<IReadOnlyList<RatingDto>> HandleAsync(GetRatingByAnnouncementId query,
        CancellationToken cancellationToken = default)
    {
        var id = new Id(query.AnnouncementId);

        var ratings = await db.Ratings
            .AsNoTracking()
            .Where(r => r.AnnouncementId == id)
            .ToListAsync(cancellationToken);

        return ratings.Select(r => r.AsDto()).ToList();
    }
}
