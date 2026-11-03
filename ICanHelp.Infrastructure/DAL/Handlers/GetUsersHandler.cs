using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetUsersHandler : IQueryHandler<GetUsers, IReadOnlyList<UserDto>>
{
    private readonly ICanHelpDbContext _db;

    public GetUsersHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<UserDto>> HandleAsync(GetUsers query, CancellationToken cancellationToken = default)
    {
        var (skip, take) = Extensions.ToSkipTake(query.Page, query.PageSize);

        var users = await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return users.Select(u => u.AsDto()).ToList();
    }
}
