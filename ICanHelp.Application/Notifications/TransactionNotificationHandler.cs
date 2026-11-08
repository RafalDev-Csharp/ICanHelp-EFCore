using ICanHelp.Application.DTO;
using ICanHelp.Application.Events;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Events;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Notifications;

/// <summary>
/// Krok 13: powiadomienia o transakcji – oferty, wiadomości, edycja ogłoszenia. Tu jest tylko wybór odbiorcy
/// (zawsze druga strona niż ta, która działała) i parametry tekstu; zapis i dostarczenie robi <see cref="NotificationPublisher"/>.
/// <list type="bullet">
/// <item>OfferMade → właściciel; OfferCountered / OfferWithdrawn → druga strona niż autor oferty</item>
/// <item>OfferAccepted / OfferRejected → autor oferty; odrzucona automatycznie (wybrano innego) → pomocnik</item>
/// <item>MessageSent → druga osoba w wątku; AnnouncementEdited → pomocnicy z wątkami (każda oferta zakłada wątek)</item>
/// </list>
/// Ogłoszenia już nie ma (usunięte) → nic nie wysyłamy.
/// </summary>
internal sealed class TransactionNotificationHandler :
    IDomainEventHandler<OfferMade>,
    IDomainEventHandler<OfferCountered>,
    IDomainEventHandler<OfferAccepted>,
    IDomainEventHandler<OfferRejected>,
    IDomainEventHandler<OfferWithdrawn>,
    IDomainEventHandler<MessageSent>,
    IDomainEventHandler<AnnouncementEdited>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly IConversationRepository _conversations;
    private readonly NotificationPublisher _publisher;

    public TransactionNotificationHandler(IAnnouncementRepository announcements,
        IConversationRepository conversations, NotificationPublisher publisher)
    {
        _announcements = announcements;
        _conversations = conversations;
        _publisher = publisher;
    }

    public Task HandleAsync(Guid eventId, OfferMade e, CancellationToken cancellationToken = default)
        => OfferAsync(eventId, e.AnnouncementId, e.HelperId, actor: e.HelperId, recipient: e.OwnerId,
            NotificationType.OfferReceived, e.OccurredAt,
            new() { [NotificationPayload.Price] = new Price(e.Amount, e.Currency).ToString() }, cancellationToken);

    public Task HandleAsync(Guid eventId, OfferCountered e, CancellationToken cancellationToken = default)
        => OfferAsync(eventId, e.AnnouncementId, e.HelperId,
            actor: SideOf(e.ProposedBy, e.OwnerId, e.HelperId),
            recipient: OtherSideOf(e.ProposedBy, e.OwnerId, e.HelperId),
            NotificationType.OfferCountered, e.OccurredAt,
            new()
            {
                [NotificationPayload.Price] = new Price(e.Amount, e.Currency).ToString(),
                [NotificationPayload.ProposedBy] = e.ProposedBy.ToString()
            }, cancellationToken);

    public Task HandleAsync(Guid eventId, OfferAccepted e, CancellationToken cancellationToken = default)
        => OfferAsync(eventId, e.AnnouncementId, e.HelperId,
            actor: OtherSideOf(e.ProposedBy, e.OwnerId, e.HelperId),
            recipient: SideOf(e.ProposedBy, e.OwnerId, e.HelperId),
            NotificationType.OfferAccepted, e.OccurredAt, new(), cancellationToken);

    public Task HandleAsync(Guid eventId, OfferRejected e, CancellationToken cancellationToken = default)
        => e.AnotherOfferAccepted
            // Właściciel wybrał innego pomocnika – dowiaduje się ten pomocnik (także gdy czekała kontroferta właściciela).
            ? OfferAsync(eventId, e.AnnouncementId, e.HelperId, actor: e.OwnerId, recipient: e.HelperId,
                NotificationType.OfferRejected, e.OccurredAt,
                new() { [NotificationPayload.Reason] = NotificationPayload.AnotherOfferAccepted }, cancellationToken)
            : OfferAsync(eventId, e.AnnouncementId, e.HelperId,
                actor: OtherSideOf(e.ProposedBy, e.OwnerId, e.HelperId),
                recipient: SideOf(e.ProposedBy, e.OwnerId, e.HelperId),
                NotificationType.OfferRejected, e.OccurredAt, new(), cancellationToken);

    public Task HandleAsync(Guid eventId, OfferWithdrawn e, CancellationToken cancellationToken = default)
        => OfferAsync(eventId, e.AnnouncementId, e.HelperId,
            actor: SideOf(e.ProposedBy, e.OwnerId, e.HelperId),
            recipient: OtherSideOf(e.ProposedBy, e.OwnerId, e.HelperId),
            NotificationType.OfferWithdrawn, e.OccurredAt, new(), cancellationToken);

    public async Task HandleAsync(Guid eventId, MessageSent e, CancellationToken cancellationToken = default)
    {
        var title = await GetTitleAsync(e.AnnouncementId, cancellationToken);
        if (title is null)
        {
            return;
        }

        var recipient = e.SenderId == e.ClientId ? e.HelperId : e.ClientId;

        await _publisher.PublishAsync(eventId, new UserId(e.SenderId), e.OccurredAt,
        [
            new NotificationDraft(new UserId(recipient), NotificationType.MessageReceived, new Id(e.AnnouncementId),
                new Id(e.ConversationId), new Dictionary<string, string>
                {
                    [NotificationPayload.AnnouncementTitle] = title,
                    [NotificationPayload.MessagePreview] = MyConversationDto.ToPreview(e.Text)
                })
        ], cancellationToken);
    }

    public async Task HandleAsync(Guid eventId, AnnouncementEdited e, CancellationToken cancellationToken = default)
    {
        var title = await GetTitleAsync(e.AnnouncementId, cancellationToken);
        if (title is null)
        {
            return;
        }

        var participants = await _conversations.GetParticipantsAsync(new Id(e.AnnouncementId), cancellationToken);
        var parameters = new Dictionary<string, string>
        {
            [NotificationPayload.AnnouncementTitle] = title,
            [NotificationPayload.Revision] = e.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [NotificationPayload.ChangedFields] = e.ChangedFields.ToString()
        };

        await _publisher.PublishAsync(eventId, new UserId(e.OwnerId), e.OccurredAt,
            participants.Select(p => new NotificationDraft(p.HelperId, NotificationType.AnnouncementEdited,
                new Id(e.AnnouncementId), p.ConversationId, parameters)),
            cancellationToken);
    }

    // ---------- helpers ----------

    /// <summary>The user on the given side of the negotiation (Client = owner).</summary>
    private static Guid SideOf(OfferParty party, Guid ownerId, Guid helperId)
        => party == OfferParty.Client ? ownerId : helperId;

    private static Guid OtherSideOf(OfferParty party, Guid ownerId, Guid helperId)
        => party == OfferParty.Client ? helperId : ownerId;

    private async Task OfferAsync(Guid eventId, Guid announcementId, Guid helperId, Guid actor, Guid recipient,
        NotificationType type, DateTime occurredAtUtc, Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        if (actor == recipient)
        {
            return;
        }

        var title = await GetTitleAsync(announcementId, cancellationToken);
        if (title is null)
        {
            return;
        }

        // Every offer lives in the conversation of this helper – the notification opens it.
        var conversation = await _conversations.GetByParticipantsAsync(new Id(announcementId), new UserId(helperId),
            cancellationToken);

        parameters[NotificationPayload.AnnouncementTitle] = title;

        await _publisher.PublishAsync(eventId, new UserId(actor), occurredAtUtc,
        [
            new NotificationDraft(new UserId(recipient), type, new Id(announcementId), conversation?.Id, parameters)
        ], cancellationToken);
    }

    private async Task<string?> GetTitleAsync(Guid announcementId, CancellationToken cancellationToken)
        => (await _announcements.GetByIdAsync(new Id(announcementId), cancellationToken))?.Title.Value;
}
