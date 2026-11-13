using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Services;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

/// <summary>
/// F11 G: statystyki profilu. Rola w zleceniu: właściciel ogłoszenia = Client, autor przyjętej oferty = Helper.
/// Kilka małych zapytań (oceny, zakończone, wyjścia) – przy tej skali prościej niż jedno złożone.
/// F11 H: średnia i liczniki z <see cref="ReputationPolicy"/> (jeden głos na osobę = średnia jej ocen, bez podejrzanych zleceń).
/// </summary>
internal sealed class GetUserProfileHandler : IQueryHandler<GetUserProfile, UserProfileDto?>
{
    private readonly ICanHelpDbContext _db;

    public GetUserProfileHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<UserProfileDto?> HandleAsync(GetUserProfile query, CancellationToken cancellationToken = default)
    {
        var userId = new UserId(query.UserId);

        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        // Oceny otrzymane (ratee = user) z właścicielem ogłoszenia – rola.
        var ratings = await (
                from r in _db.Ratings.AsNoTracking()
                where r.RateeId == userId
                join a in _db.Announcements.AsNoTracking() on r.AnnouncementId equals a.Id
                select new { r.AnnouncementId, r.RaterId, r.Rate, a.OwnerId })
            .ToListAsync(cancellationToken);

        // Zakończone zlecenia w obu rolach: jako zleceniodawca – moje ogłoszenia; jako pomocnik – przyjęta oferta moja.
        var completed = await (
                from a in _db.Announcements.AsNoTracking()
                where a.Status == AnnouncementStatus.Completed
                // Przyjęta oferta jest najwyżej jedna (jak w GetMyPendingRatingsHandler).
                join o in _db.Offers.AsNoTracking().Where(o => o.Status == OfferStatus.Accepted)
                    on a.Id equals o.AnnouncementId
                where a.OwnerId == userId || o.HelperId == userId
                select new { a.Id, a.OwnerId, o.HelperId, a.AcceptedAt, a.CompletedAt })
            .ToListAsync(cancellationToken);

        // F11 H: rola + partner; podejrzane zlecenia liczone z obu ról (para mogłaby zamieniać się rolami).
        var jobs = completed
            .Select(j => new
            {
                AsHelper = j.OwnerId != userId,
                Job = new FinishedJob(j.Id, j.OwnerId == userId ? j.HelperId : j.OwnerId, j.AcceptedAt, j.CompletedAt)
            })
            .ToList();
        var suspicious = ReputationPolicy.FindSuspiciousJobs(jobs.Select(j => j.Job));

        // Wyjścia ze zleceń, w których użytkownik był stroną (pomocnik z wpisu albo właściciel ogłoszenia).
        var exits = await (
                from e in _db.JobExits.AsNoTracking()
                join a in _db.Announcements.AsNoTracking() on e.AnnouncementId equals a.Id
                where e.HelperId == userId || a.OwnerId == userId
                select new { Exit = e, a.OwnerId, a.Title })
            .ToListAsync(cancellationToken);

        RoleStatsDto Stats(bool asHelper)
        {
            var reputation = ReputationPolicy.Summarize(
                jobs.Where(j => j.AsHelper == asHelper).Select(j => j.Job).ToList(),
                ratings
                    .Where(r => (r.OwnerId != userId) == asHelper)
                    .Select(r => new ReceivedRating(r.AnnouncementId, r.RaterId, r.Rate.Value))
                    .ToList(),
                suspicious);
            var mine = exits.Where(x => asHelper ? x.Exit.HelperId == userId : x.OwnerId == userId).ToList();
            int Count(JobExitKind kind, bool byUser)
                => mine.Count(x => x.Exit.Kind == kind && (x.Exit.ActorId == userId) == byUser);

            return new RoleStatsDto
            {
                AverageRating = reputation.AverageRating,
                RatingCount = reputation.RatingCount,
                RaterCount = reputation.RaterCount,
                CompletedCount = reputation.CompletedCount,
                PartnerCount = reputation.PartnerCount,
                MostJobsWithOnePartner = reputation.MostJobsWithOnePartner,
                InterruptedByUser = Count(JobExitKind.Interrupted, byUser: true),
                InterruptedByOther = Count(JobExitKind.Interrupted, byUser: false),
                AbandonedByUser = Count(JobExitKind.Abandoned, byUser: true),
                AbandonedByOther = Count(JobExitKind.Abandoned, byUser: false)
            };
        }

        return new UserProfileDto
        {
            User = user.AsPublicDto(),
            AsHelper = Stats(asHelper: true),
            AsClient = Stats(asHelper: false),
            Interruptions = exits
                .Where(x => x.Exit.Kind == JobExitKind.Interrupted)
                .OrderByDescending(x => x.Exit.OccurredAt)
                .Select(x => new ProfileInterruptionDto
                {
                    AnnouncementId = x.Exit.AnnouncementId,
                    AnnouncementTitle = x.Title,
                    Role = (x.OwnerId == userId ? OfferParty.Client : OfferParty.Helper).ToString(),
                    ByUser = x.Exit.ActorId == userId,
                    Exit = x.Exit.AsDto()
                })
                .ToList()
        };
    }
}
