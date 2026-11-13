using System.Globalization;
using ICanHelp.Core.Enums;

namespace ICanHelp.Application.Notifications;

/// <summary>Title and body of a notification in Polish (list in the app, toast, push).</summary>
public sealed record NotificationText(string Title, string Body);

/// <summary>
/// Krok 15: the text of a notification from <see cref="NotificationType"/> + parameters
/// (<see cref="NotificationPayload"/>). One place for the push message and <c>NotificationDto.Title/Body</c>,
/// so the app does not have to build the texts itself. The texts are the same as the ones the web app had
/// (<c>notification-texts.ts</c>), e.g. „Nowa oferta: 150 zł” + „Cieknący kran”.
/// </summary>
public static class NotificationTextFormatter
{
    // No grouping below 10 000 – the same as Intl.NumberFormat("pl-PL") in the browser ("1500 zł", "12 500 zł").
    private const char NonBreakingSpace = ' ';

    private static readonly Dictionary<string, string> FieldLabels = new()
    {
        ["Category"] = "kategoria",
        ["Difficulty"] = "trudność",
        ["Title"] = "tytuł",
        ["Description"] = "opis",
        ["Budget"] = "budżet",
        ["Location"] = "miejsce",
        ["Images"] = "zdjęcia"
    };

    public static NotificationText Format(NotificationType type, IReadOnlyDictionary<string, string> parameters)
    {
        var title = Get(parameters, NotificationPayload.AnnouncementTitle);
        var price = FormatPrice(Get(parameters, NotificationPayload.Price));

        switch (type)
        {
            case NotificationType.OfferReceived:
                return new(price.Length > 0 ? $"Nowa oferta: {price}" : "Nowa oferta", title);
            case NotificationType.OfferCountered:
                return new(price.Length > 0 ? $"Kontroferta: {price}" : "Kontroferta", title);
            case NotificationType.OfferRevised:
                return new(price.Length > 0 ? $"Oferta zaktualizowana: {price}" : "Oferta zaktualizowana", title);
            case NotificationType.OfferAccepted:
                return new("Twoja oferta została przyjęta", title);
            case NotificationType.OfferRejected:
                return Get(parameters, NotificationPayload.Reason) == NotificationPayload.AnotherOfferAccepted
                    ? new("Zleceniodawca wybrał innego pomocnika", title)
                    : new("Twoja oferta została odrzucona", title);
            case NotificationType.OfferWithdrawn:
                return new("Oferta wycofana", title);
            case NotificationType.JobStarted:
                return new("Pomocnik rozpoczął zlecenie", title);
            case NotificationType.JobDoneReported:
                return new("Pomocnik zgłosił wykonanie – potwierdź", title);
            case NotificationType.JobCompleted:
                return new("Zlecenie zakończone – możesz ocenić", title);
            case NotificationType.JobAbandoned:
                return new($"{ActorLabel(parameters)} zrezygnował ze zlecenia", ExitBody(title, parameters));
            case NotificationType.JobInterrupted:
                return new($"{ActorLabel(parameters)} przerwał zlecenie", ExitBody(title, parameters));
            case NotificationType.MessageReceived:
                return new(title.Length > 0 ? $"Nowa wiadomość: {title}" : "Nowa wiadomość",
                    Get(parameters, NotificationPayload.MessagePreview));
            case NotificationType.AnnouncementEdited:
            {
                var fields = ChangedFields(Get(parameters, NotificationPayload.ChangedFields));
                return new(title.Length > 0 ? $"Ogłoszenie zmienione: {title}" : "Ogłoszenie zmienione",
                    fields.Length > 0
                        ? $"Zmieniono: {fields}. Sprawdź, czy Twoja oferta jest aktualna."
                        : "Sprawdź szczegóły ogłoszenia.");
            }
            case NotificationType.NewAnnouncementNearby:
            {
                var where = Get(parameters, NotificationPayload.AlertKind) == nameof(AlertKind.Route)
                    ? "po drodze"
                    : "w okolicy";
                var alertName = Get(parameters, NotificationPayload.AlertName);
                var details = string.Join(" · ", new[]
                {
                    FormatDistance(Get(parameters, NotificationPayload.DistanceKm)),
                    Get(parameters, NotificationPayload.CategoryName),
                    alertName.Length > 0 ? $"alert „{alertName}”" : string.Empty
                }.Where(part => part.Length > 0));

                return new($"Nowe ogłoszenie {where}", details.Length > 0 ? $"{title} – {details}" : title);
            }
            case NotificationType.NewAnnouncementsDigest:
            {
                var count = Get(parameters, NotificationPayload.Count);
                return new($"Kolejne nowe ogłoszenia ({(count.Length > 0 ? count : "…")})",
                    "Pasują do Twoich alertów – zobacz listę ogłoszeń.");
            }
            default:
                return new("Powiadomienie", title);
        }
    }

    /// <summary>
    /// Where a click leads in the app (the same rule as the web app): offers and messages – the conversation,
    /// an edit and an alert – the announcement, the digest – the list of announcements; nothing to open → null.
    /// </summary>
    public static string? Link(NotificationType type, Guid? announcementId, Guid? conversationId)
    {
        var announcement = announcementId is { } a ? $"/announcements/{a}" : null;

        return type switch
        {
            NotificationType.AnnouncementEdited or NotificationType.NewAnnouncementNearby => announcement,
            NotificationType.NewAnnouncementsDigest => "/announcements",
            _ => conversationId is { } c ? $"/conversations/{c}" : announcement
        };
    }

    /// <summary><c>"120.00 PLN"</c> (<c>Price.ToString()</c>) → „120 zł”, „99,50 zł”; another currency – as it is.</summary>
    public static string FormatPrice(string value)
    {
        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0
            || !decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            || (parts.Length > 1 && parts[1] != "PLN"))
        {
            return value.Trim();
        }

        var number = amount == decimal.Truncate(amount)
            ? FormatNumber(amount, "0")
            : FormatNumber(amount, "0.00");

        return $"{number}{NonBreakingSpace}zł";
    }

    /// <summary>Kilometres (<c>"2.46"</c>) → „2,5 km”; below 1 km – metres („350 m”), from 10 km – whole km.</summary>
    public static string FormatDistance(string value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var km)
            || !double.IsFinite(km) || km < 0)
        {
            return string.Empty;
        }

        var meters = Math.Max(10, Math.Round(km * 1000 / 10, MidpointRounding.AwayFromZero) * 10);
        if (meters < 1000)
        {
            return $"{meters.ToString("0", CultureInfo.InvariantCulture)} m";
        }

        var shown = km < 10 ? Math.Round(km, 1, MidpointRounding.AwayFromZero) : Math.Round(km, MidpointRounding.AwayFromZero);
        return $"{FormatNumber((decimal)shown, "0.#")} km";
    }

    private static string FormatNumber(decimal value, string format)
    {
        var text = value.ToString(format, CultureInfo.InvariantCulture).Replace('.', ',');
        if (Math.Abs(value) < 10_000)
        {
            return text;
        }

        // Thousands separated by a non-breaking space (pl-PL), e.g. "12 500".
        var negative = text.StartsWith('-');
        var digits = negative ? text[1..] : text;
        var comma = digits.IndexOf(',');
        var whole = comma < 0 ? digits : digits[..comma];
        var fraction = comma < 0 ? string.Empty : digits[comma..];

        var grouped = new System.Text.StringBuilder();
        for (var i = 0; i < whole.Length; i++)
        {
            if (i > 0 && (whole.Length - i) % 3 == 0)
            {
                grouped.Append(NonBreakingSpace);
            }

            grouped.Append(whole[i]);
        }

        return (negative ? "-" : string.Empty) + grouped + fraction;
    }

    /// <summary>F11 E: the reasons as in the web app (<c>job-exit.ts</c>).</summary>
    public static readonly IReadOnlyDictionary<string, string> ExitReasonLabels = new Dictionary<string, string>
    {
        [nameof(JobExitReason.CannotMakeIt)] = "nie mogę w tym terminie",
        [nameof(JobExitReason.TermsDiffer)] = "warunki inne niż w ogłoszeniu",
        [nameof(JobExitReason.NoContact)] = "brak kontaktu",
        [nameof(JobExitReason.Disagreement)] = "konflikt / nie dogadaliśmy się",
        [nameof(JobExitReason.Other)] = "inny"
    };

    private static string ActorLabel(IReadOnlyDictionary<string, string> parameters)
        => Get(parameters, NotificationPayload.ActorRole) == nameof(OfferParty.Client) ? "Zleceniodawca" : "Pomocnik";

    // „Cieknący kran – powód: brak kontaktu”; „Inny” → sam opis („powód: Sam naprawiłem.”).
    private static string ExitBody(string title, IReadOnlyDictionary<string, string> parameters)
    {
        var reason = Get(parameters, NotificationPayload.ExitReason);
        var note = Get(parameters, NotificationPayload.Note);
        var label = reason == nameof(JobExitReason.Other) && note.Length > 0
            ? note
            : ExitReasonLabels.GetValueOrDefault(reason, reason);

        if (reason != nameof(JobExitReason.Other) && note.Length > 0)
        {
            label = $"{label} ({note})";
        }

        if (label.Length == 0)
        {
            return title;
        }

        return title.Length > 0 ? $"{title} – powód: {label}" : $"Powód: {label}";
    }

    private static string ChangedFields(string value)
        => string.Join(", ", value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(field => field != "None")
            .Select(field => FieldLabels.GetValueOrDefault(field, field)));

    private static string Get(IReadOnlyDictionary<string, string> parameters, string key)
        => parameters.TryGetValue(key, out var value) ? value : string.Empty;
}
