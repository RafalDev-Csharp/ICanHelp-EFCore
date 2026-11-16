using System.Globalization;
using ICanHelp.Application.Alerts;
using ICanHelp.Application.Events;
using ICanHelp.Application.Time;
using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Notifications;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Events;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Notifications;

/// <summary>
/// Krok 14: nowe ogłoszenie → powiadomienia dla właścicieli pasujących alertów (obszar / trasa).
/// <list type="bullet">
/// <item>geometria w bazie (<see cref="IAnnouncementAlertRepository.FindCandidatesAsync"/>: strefa alertu zawiera punkt
/// ogłoszenia, bez alertów właściciela ogłoszenia), podkategorie i harmonogram – domena;</item>
/// <item>jedno powiadomienie na (ogłoszenie, użytkownik), nawet przy kilku pasujących alertach – bierzemy najbliższy;</item>
/// <item>dzienny limit (<see cref="AlertOptions.DailyLimit"/>, doba lokalna): powyżej – jeden skrót dnia
/// (<see cref="NotificationType.NewAnnouncementsDigest"/>, „i jeszcze N nowych”), aktualizowany zamiast nowych wpisów;</item>
/// <item>cisza nocna: powiadomienie jest zapisane (lista, licznik), ale nie idzie na żywo.</item>
/// </list>
/// Ogłoszenie usunięte albo już nie Open (np. anulowane, zanim outbox doszedł) → nic.
/// </summary>
internal sealed class NewAnnouncementAlertHandler(IAnnouncementRepository announcements, ICategoryRepository categories,
    IAnnouncementAlertRepository alerts, INotificationRepository notifications, IUnitOfWork unitOfWork,
    INotificationSender sender, IClock clock, AlertOptions options) : IDomainEventHandler<AnnouncementPublished>
{
    public async Task HandleAsync(Guid eventId, AnnouncementPublished e, CancellationToken cancellationToken = default)
    {
        var announcement = await announcements.GetByIdAsync(new Id(e.AnnouncementId), cancellationToken);
        if (announcement is null || announcement.Status != AnnouncementStatus.Open)
        {
            return;
        }

        var timeZone = options.GetTimeZone();
        var publishedLocal = TimeZoneInfo.ConvertTimeFromUtc(e.OccurredAt.AsUtc(), timeZone);

        var matches = await FindMatchesAsync(e, publishedLocal, cancellationToken);
        if (matches.Count == 0)
        {
            return;
        }

        var categoryName = (await categories.GetByIdAsync(new Id(e.CategoryId), cancellationToken))?.CategoryName.Value
                           ?? string.Empty;
        var dayStartUtc = StartOfLocalDayUtc(publishedLocal, timeZone);
        var toDeliver = new List<Notification>();

        await unitOfWork.ExecuteAsync(async () =>
        {
            toDeliver.Clear();

            foreach (var match in matches)
            {
                var userId = match.Alert.UserId;

                // Idempotencja: to samo zdarzenie drugi raz (ponowienie z outboksa) nic nie dodaje.
                if (await notifications.ExistsAsync(userId, eventId, cancellationToken))
                {
                    continue;
                }

                var sentToday = await notifications.CountSinceAsync(userId, NotificationType.NewAnnouncementNearby,
                    dayStartUtc, cancellationToken);

                toDeliver.Add(sentToday < options.DailyLimit
                    ? await AddNearbyAsync(match, announcement, categoryName, eventId, e.OccurredAt, cancellationToken)
                    : await AddToDigestAsync(userId, dayStartUtc, eventId, e.OccurredAt, cancellationToken));
            }
        }, cancellationToken);

        // Cisza nocna: zostaje wpis na liście, bez dostarczenia na żywo.
        if (options.IsQuietAt(TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow.AsUtc(), timeZone)))
        {
            return;
        }

        foreach (var notification in toDeliver)
        {
            await sender.SendAsync(notification.UserId, notification.ToDto(), cancellationToken);
        }
    }

    /// <summary>
    /// Alerty, do których ogłoszenie pasuje (strefa – baza; kategoria, podkategorie i harmonogram – domena),
    /// po jednym na użytkownika: ten, od którego kształtu jest najbliżej.
    /// </summary>
    private async Task<List<AlertCandidate>> FindMatchesAsync(AnnouncementPublished e, DateTime publishedLocal,
        CancellationToken cancellationToken)
    {
        var ownerId = new UserId(e.OwnerId);
        var categoryId = new Id(e.CategoryId);
        var subcategoryId = new Id(e.SubcategoryId);

        var candidates = await alerts.FindCandidatesAsync(categoryId, ownerId, new Location(e.Latitude, e.Longitude),
            cancellationToken);

        return candidates
            .Where(c => !c.Alert.IsOwnedBy(ownerId)
                        && c.Alert.Matches(categoryId, subcategoryId)
                        && c.Alert.IsActiveAt(publishedLocal))
            .GroupBy(c => c.Alert.UserId)
            .Select(g => g.OrderBy(c => c.DistanceMeters).First())
            .ToList();
    }

    private async Task<Notification> AddNearbyAsync(AlertCandidate match, Announcement announcement,
        string categoryName, Guid eventId, DateTime occurredAtUtc, CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, string>
        {
            [NotificationPayload.AnnouncementTitle] = announcement.Title.Value,
            [NotificationPayload.CategoryName] = categoryName,
            [NotificationPayload.AlertName] = match.Alert.Name.Value,
            [NotificationPayload.AlertKind] = match.Alert.Kind.ToString()
        };

        // Cała Polska nie ma środka ani trasy – odległość nic by nie mówiła.
        if (match.Alert.Kind != AlertKind.Country)
        {
            payload[NotificationPayload.DistanceKm] = FormatKm(match.DistanceMeters);
        }

        var notification = new Notification(Id.New(), match.Alert.UserId, NotificationType.NewAnnouncementNearby,
            NotificationPayload.Serialize(payload), announcement.Id, conversationId: null, eventId, occurredAtUtc);

        await notifications.AddAsync(notification, cancellationToken);
        return notification;
    }

    /// <summary>Ponad limit: jeden skrót dnia na użytkownika – nowy albo dzisiejszy z licznikiem o 1 większym.</summary>
    private async Task<Notification> AddToDigestAsync(UserId userId, DateTime dayStartUtc, Guid eventId,
        DateTime occurredAtUtc, CancellationToken cancellationToken)
    {
        var digest = await notifications.GetLatestSinceAsync(userId, NotificationType.NewAnnouncementsDigest,
            dayStartUtc, cancellationToken);

        if (digest is null)
        {
            digest = new Notification(Id.New(), userId, NotificationType.NewAnnouncementsDigest, DigestPayload(1),
                announcementId: null, conversationId: null, eventId, occurredAtUtc);
            await notifications.AddAsync(digest, cancellationToken);
        }
        else
        {
            // Ponowienie tego samego zdarzenia mogłoby tu podbić licznik drugi raz (źródłem skrótu jest
            // pierwsze zdarzenie) – rzadkie i niegroźne, to tylko „i jeszcze N”.
            digest.Renew(DigestPayload(DigestCount(digest) + 1), occurredAtUtc);
        }

        return digest;
    }

    private static string FormatKm(double meters)
        => Math.Round(meters / 1000, 1).ToString("0.0", CultureInfo.InvariantCulture);

    private static string DigestPayload(int count)
        => NotificationPayload.Serialize(new Dictionary<string, string>
        {
            [NotificationPayload.Count] = count.ToString(CultureInfo.InvariantCulture)
        });

    private static int DigestCount(Notification digest)
        => NotificationPayload.Deserialize(digest.Payload).TryGetValue(NotificationPayload.Count, out var value)
           && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            ? count
            : 0;

    private static DateTime StartOfLocalDayUtc(DateTime local, TimeZoneInfo timeZone)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.Date, DateTimeKind.Unspecified), timeZone);
}
