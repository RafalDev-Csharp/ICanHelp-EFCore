using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetLikedUsersHandler : IQueryHandler<GetLikedUsers, IReadOnlyList<LikedUserDto>>
{
    private readonly ICanHelpDbContext _db;

    public GetLikedUsersHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<LikedUserDto>> HandleAsync(GetLikedUsers query,
        CancellationToken cancellationToken = default)
    {
        var id = new UserId(query.UserId);

        var likes = await _db.LikedUsers
            .AsNoTracking()
            .Where(l => l.LikerId == id)
            .OrderByDescending(l => l.LikedAt)
            .ToListAsync(cancellationToken);

        return likes.Select(l => l.AsDto()).ToList();
    }
}
