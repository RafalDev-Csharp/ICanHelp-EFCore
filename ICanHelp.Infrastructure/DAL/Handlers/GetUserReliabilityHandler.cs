using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.Enums;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

/// <summary>F11 E: wyjścia ze zleceń, które użytkownik zrobił sam (indeks JobExits.ActorId).</summary>
internal sealed class GetUserReliabilityHandler : IQueryHandler<GetUserReliability, UserReliabilityDto?>
{
    private readonly ICanHelpDbContext _db;

    public GetUserReliabilityHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<UserReliabilityDto?> HandleAsync(GetUserReliability query,
        CancellationToken cancellationToken = default)
    {
        var userId = new UserId(query.UserId);

        if (!await _db.Users.AnyAsync(u => u.Id == userId, cancellationToken))
        {
            return null;
        }

        var counts = await _db.JobExits
            .AsNoTracking()
            .Where(e => e.ActorId == userId)
            .GroupBy(e => e.Kind)
            .Select(g => new { Kind = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return new UserReliabilityDto
        {
            UserId = query.UserId,
            AbandonedCount = counts.Where(c => c.Kind == JobExitKind.Abandoned).Sum(c => c.Count),
            InterruptedCount = counts.Where(c => c.Kind == JobExitKind.Interrupted).Sum(c => c.Count)
        };
    }
}
