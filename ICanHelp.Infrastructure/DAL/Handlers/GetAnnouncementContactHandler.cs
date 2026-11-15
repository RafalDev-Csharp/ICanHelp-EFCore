using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.Enums;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetAnnouncementContactHandler(ICanHelpDbContext db)
    : IQueryHandler<GetAnnouncementContact, AnnouncementContactDto?>
{
    public async Task<AnnouncementContactDto?> HandleAsync(GetAnnouncementContact query,
        CancellationToken cancellationToken = default)
    {
        var id = new Id(query.AnnouncementId);
        var userId = new UserId(query.UserId);

        var announcement = await db.Announcements
            .AsNoTracking()
            .Include(a => a.Offers)
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

        // Kontakt dopiero po akceptacji oferty (anulowane i otwarte – nie). Po przerwaniu (F11 E) – tak: strony
        // rozliczają się między sobą za wykonaną część.
        if (announcement is null || announcement.Status is not
                (AnnouncementStatus.Accepted or AnnouncementStatus.InProgress or AnnouncementStatus.Completed
                or AnnouncementStatus.Interrupted))
        {
            return null;
        }

        // Ktoś spoza transakcji dostaje to samo co przy braku ogłoszenia (nie zdradzamy niczego).
        if (!announcement.IsParticipant(userId))
        {
            return null;
        }

        var otherId = userId == announcement.OwnerId ? announcement.GetAcceptedHelperId()! : announcement.OwnerId;

        var other = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == otherId, cancellationToken);

        return other is null
            ? null
            : new AnnouncementContactDto
            {
                UserId = other.Id,
                UserName = other.UserName,
                FullName = other.FullName,
                ContactData = other.ContactData.AsDto()
            };
    }
}
