using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.Enums;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

/// <summary>Otrzymane oceny z tytułem ogłoszenia i rolą czytającego (indeks Ratings.RateeId).</summary>
internal sealed class GetMyRatingsHandler : IQueryHandler<GetMyRatings, IReadOnlyList<MyRatingDto>>
{
    private readonly ICanHelpDbContext _db;

    public GetMyRatingsHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MyRatingDto>> HandleAsync(GetMyRatings query,
        CancellationToken cancellationToken = default)
    {
        var userId = new UserId(query.UserId);

        var rows = await (
                from r in _db.Ratings.AsNoTracking()
                where r.RateeId == userId
                join a in _db.Announcements.AsNoTracking() on r.AnnouncementId equals a.Id
                join rater in _db.Users.AsNoTracking() on r.RaterId equals rater.Id
                orderby r.CreatedAt descending
                select new { Rating = r, a.Title, a.OwnerId, RaterUserName = rater.UserName })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new MyRatingDto
            {
                Id = row.Rating.Id,
                AnnouncementId = row.Rating.AnnouncementId,
                AnnouncementTitle = row.Title,
                MyRole = MyRatingDto.RoleOf(row.OwnerId, query.UserId),
                Rater = new ConversationPartyDto { Id = row.Rating.RaterId, UserName = row.RaterUserName },
                Rate = row.Rating.Rate,
                Comment = row.Rating.Comment?.Value,
                CreatedAt = row.Rating.CreatedAt
            })
            .ToList();
    }
}

/// <summary>
/// Zakończone ogłoszenia, w których czytający był stroną (właściciel albo autor przyjętej oferty), bez jego oceny.
/// Przyjęta oferta jest w ogłoszeniu najwyżej jedna – stąd złączenie po statusie zamiast po <c>AcceptedOfferId</c>.
/// </summary>
internal sealed class GetMyPendingRatingsHandler : IQueryHandler<GetMyPendingRatings, IReadOnlyList<PendingRatingDto>>
{
    private readonly ICanHelpDbContext _db;

    public GetMyPendingRatingsHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PendingRatingDto>> HandleAsync(GetMyPendingRatings query,
        CancellationToken cancellationToken = default)
    {
        var userId = new UserId(query.UserId);

        var rows = await (
                from a in _db.Announcements.AsNoTracking()
                where a.Status == AnnouncementStatus.Completed
                join o in _db.Offers.AsNoTracking().Where(o => o.Status == OfferStatus.Accepted)
                    on a.Id equals o.AnnouncementId
                where a.OwnerId == userId || o.HelperId == userId
                where !_db.Ratings.Any(r => r.AnnouncementId == a.Id && r.RaterId == userId)
                join owner in _db.Users.AsNoTracking() on a.OwnerId equals owner.Id
                join helper in _db.Users.AsNoTracking() on o.HelperId equals helper.Id
                orderby a.CreatedAt descending
                select new
                {
                    a.Id,
                    a.Title,
                    a.OwnerId,
                    a.CreatedAt,
                    o.HelperId,
                    OwnerUserName = owner.UserName,
                    HelperUserName = helper.UserName,
                    ThumbnailId = a.Images.OrderBy(i => i.AddedAt).Select(i => i.Id).FirstOrDefault()
                })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row =>
            {
                var isOwner = row.OwnerId == userId;
                return new PendingRatingDto
                {
                    AnnouncementId = row.Id,
                    AnnouncementTitle = row.Title,
                    ThumbnailUrl = row.ThumbnailId is null ? null : ImageDto.GetUrl(row.Id, row.ThumbnailId),
                    MyRole = MyRatingDto.RoleOf(row.OwnerId, query.UserId),
                    OtherParty = isOwner
                        ? new ConversationPartyDto { Id = row.HelperId, UserName = row.HelperUserName }
                        : new ConversationPartyDto { Id = row.OwnerId, UserName = row.OwnerUserName },
                    CreatedAt = row.CreatedAt
                };
            })
            .ToList();
    }
}
