using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetRatingsByUserHandler : IQueryHandler<GetRatingsByUser, IReadOnlyList<RatingDto>>
{
    private readonly ICanHelpDbContext _db;

    public GetRatingsByUserHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<RatingDto>> HandleAsync(GetRatingsByUser query,
        CancellationToken cancellationToken = default)
    {
        var id = new UserId(query.UserId);

        var ratings = await _db.Ratings
            .AsNoTracking()
            .Where(r => r.RateeId == id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return ratings.Select(r => r.AsDto()).ToList();
    }
}
