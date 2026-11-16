using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetUserPublicByIdHandler(ICanHelpDbContext db) : IQueryHandler<GetUserPublicById, UserPublicDto?>
{
    public async Task<UserPublicDto?> HandleAsync(GetUserPublicById query, CancellationToken cancellationToken = default)
    {
        var id = new UserId(query.UserId);

        var user = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

        return user?.AsPublicDto();
    }
}
