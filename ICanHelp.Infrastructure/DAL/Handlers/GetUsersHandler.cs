using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetUsersHandler(ICanHelpDbContext db) : IQueryHandler<GetUsers, IReadOnlyList<UserDto>>
{
    public async Task<IReadOnlyList<UserDto>> HandleAsync(GetUsers query, CancellationToken cancellationToken = default)
    {
        var users = await db.Users
            .AsNoTracking()
            .OrderBy(u => u.CreatedAt)
            .TakePage(query.Page, query.PageSize)
            .ToListAsync(cancellationToken);

        return users.Select(u => u.AsDto()).ToList();
    }
}
