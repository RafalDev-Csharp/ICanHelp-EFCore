using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetAnnouncementByIdHandler : IQueryHandler<GetAnnouncementById, AnnouncementDetailsDto?>
{
    private readonly ICanHelpDbContext _db;

    public GetAnnouncementByIdHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<AnnouncementDetailsDto?> HandleAsync(GetAnnouncementById query,
        CancellationToken cancellationToken = default)
    {
        var id = new Id(query.AnnouncementId);

        var announcement = await _db.Announcements
            .AsNoTracking()
            .Include(a => a.Offers)
            .Include(a => a.Images)
            .AsSplitQuery()
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

        return announcement?.AsDetailsDto();
    }
}
