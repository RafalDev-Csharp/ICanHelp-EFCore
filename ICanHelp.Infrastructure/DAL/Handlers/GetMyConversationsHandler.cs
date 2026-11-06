using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.Enums;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

/// <summary>
/// „Moje negocjacje”. Jedno zapytanie SQL: wątki użytkownika (indeksy Conversations.ClientId / HelperId) + skalary
/// z podzapytań (ostatnia wiadomość – indeks ConversationId+SentAt, ostatnia oferta – indeks AnnouncementId+HelperId+Status,
/// liczniki nieprzeczytanych, ostatnia zmiana ogłoszenia – klucz AnnouncementId+Revision). Bez ładowania całych kolekcji.
/// <para>
/// Sortowanie po <c>LastActivityAt</c> (maksimum z czterech dat) i stronicowanie są w pamięci: liczba wątków jednego
/// użytkownika jest niewielka, a „największa z kilku dat” w SQL to powielone podzapytania. Gdyby to urosło –
/// zdenormalizowana kolumna <c>Conversations.LastActivityAt</c> aktualizowana przy zapisie.
/// </para>
/// </summary>
internal sealed class GetMyConversationsHandler : IQueryHandler<GetMyConversations, IReadOnlyList<MyConversationDto>>
{
    private readonly ICanHelpDbContext _db;

    public GetMyConversationsHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MyConversationDto>> HandleAsync(GetMyConversations query,
        CancellationToken cancellationToken = default)
    {
        var userId = new UserId(query.UserId);

        var conversations = _db.Conversations.AsNoTracking();
        conversations = query.Role switch
        {
            OfferParty.Client => conversations.Where(c => c.ClientId == userId),
            OfferParty.Helper => conversations.Where(c => c.HelperId == userId),
            _ => conversations.Where(c => c.ClientId == userId || c.HelperId == userId)
        };

        var announcements = _db.Announcements.AsNoTracking();
        if (query.Status is { } status)
        {
            announcements = announcements.Where(a => a.Status == status);
        }

        var rows = await (
                from c in conversations
                join a in announcements on c.AnnouncementId equals a.Id
                join client in _db.Users.AsNoTracking() on c.ClientId equals client.Id
                join helper in _db.Users.AsNoTracking() on c.HelperId equals helper.Id
                select new
                {
                    // Bez Include(Messages) – encja służy tylko do GetParty/GetLastReadAt.
                    Conversation = c,
                    Title = a.Title,
                    AnnouncementStatus = a.Status,
                    AnnouncementRevision = a.Revision,
                    ThumbnailId = a.Images.OrderBy(i => i.AddedAt).Select(i => i.Id).FirstOrDefault(),
                    ClientUserName = client.UserName,
                    HelperUserName = helper.UserName,
                    LastMessageAt = c.Messages.Max(m => (DateTime?)m.SentAt),
                    LastMessageText = c.Messages.OrderByDescending(m => m.SentAt).Select(m => m.Text).FirstOrDefault(),
                    // Nieprzeczytane dla obu stron (to samo co Conversation.CountUnread) – w pamięci wybieramy swoją.
                    ClientUnread = c.Messages.Count(m =>
                        m.SenderId == c.HelperId && (c.ClientLastReadAt == null || m.SentAt > c.ClientLastReadAt)),
                    HelperUnread = c.Messages.Count(m =>
                        m.SenderId == c.ClientId && (c.HelperLastReadAt == null || m.SentAt > c.HelperLastReadAt)),
                    LastOffer = _db.Offers
                        .Where(o => o.AnnouncementId == c.AnnouncementId && o.HelperId == c.HelperId)
                        .OrderByDescending(o => o.CreatedAt)
                        // Same skalary (bez materializacji encji z typem złożonym Price w opcjonalnym podzapytaniu).
                        .Select(o => new
                        {
                            o.Id,
                            o.Price.Amount,
                            o.Price.Currency,
                            o.Status,
                            o.ProposedBy,
                            o.AnnouncementRevision,
                            o.CreatedAt
                        })
                        .FirstOrDefault(),
                    LastChangeAt = _db.AnnouncementChanges
                        .Where(ch => ch.AnnouncementId == c.AnnouncementId)
                        .Max(ch => (DateTime?)ch.ChangedAt)
                })
            .ToListAsync(cancellationToken);

        var (skip, take) = Extensions.ToSkipTake(query.Page, query.PageSize);

        return rows
            .Select(r =>
            {
                var conversation = r.Conversation;
                var myRole = conversation.GetParty(userId);
                var isClient = myRole == OfferParty.Client;
                var lastReadAt = conversation.GetLastReadAt(userId);

                return new MyConversationDto
                {
                    ConversationId = conversation.Id,
                    AnnouncementId = conversation.AnnouncementId,
                    Title = r.Title,
                    ThumbnailUrl = r.ThumbnailId is null
                        ? null
                        : ImageDto.GetUrl(conversation.AnnouncementId, r.ThumbnailId),
                    AnnouncementStatus = r.AnnouncementStatus.ToString(),
                    MyRole = myRole.ToString(),
                    OtherParty = isClient
                        ? new ConversationPartyDto { Id = conversation.HelperId, UserName = r.HelperUserName }
                        : new ConversationPartyDto { Id = conversation.ClientId, UserName = r.ClientUserName },
                    LastOffer = r.LastOffer is { } offer
                        ? new LastOfferDto
                        {
                            Id = offer.Id,
                            Price = new PriceDto { Amount = offer.Amount, Currency = offer.Currency },
                            Status = offer.Status.ToString(),
                            ProposedBy = offer.ProposedBy.ToString(),
                            IsOutdated = offer.AnnouncementRevision != r.AnnouncementRevision,
                            CreatedAt = offer.CreatedAt
                        }
                        : null,
                    LastMessagePreview = r.LastMessageText is { } text ? MyConversationDto.ToPreview(text) : null,
                    LastActivityAt = Latest(conversation.CreatedAt, r.LastMessageAt, r.LastOffer?.CreatedAt, r.LastChangeAt),
                    UnreadCount = isClient ? r.ClientUnread : r.HelperUnread,
                    // Ostrzeżenie dla pomocnika – właściciel edytował sam. Nigdy nieczytany wątek: liczą się zmiany
                    // od jego założenia (wcześniejsze pomocnik widział, zakładając wątek).
                    AnnouncementChangedSinceLastRead =
                        !isClient && r.LastChangeAt > (lastReadAt ?? conversation.CreatedAt)
                };
            })
            .OrderByDescending(x => x.LastActivityAt)
            .ThenBy(x => x.ConversationId)
            .Skip(skip)
            .Take(take)
            .ToList();
    }

    private static DateTime Latest(DateTime first, params DateTime?[] others)
        => others.Aggregate(first, (latest, next) => next > latest ? next.Value : latest);
}
