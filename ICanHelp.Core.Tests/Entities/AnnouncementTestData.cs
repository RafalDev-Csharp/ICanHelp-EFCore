using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Enums;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Tests.Entities;

/// <summary>
/// Wspólne dane i skróty do budowania ogłoszeń w testach.
/// Czas jest stały (domena nie woła DateTime.UtcNow), więc testy są powtarzalne.
/// </summary>
internal static class AnnouncementTestData
{
    public static readonly DateTime Now = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    public static readonly UserId OwnerId = UserId.New();
    public static readonly UserId Helper = UserId.New();
    public static readonly UserId OtherHelper = UserId.New();
    public static readonly UserId Stranger = UserId.New();

    public static Announcement CreateAnnouncement(Price? budget = null)
        => new(
            Id.New(),
            OwnerId,
            Id.New(),
            Id.New(),
            DifficultyLevel.Easy,
            "Cieknący kran",
            "Kran w kuchni cieknie od tygodnia.",
            budget ?? Price.Pln(150),
            new Location(52.2297, 21.0122),
            Now);

    /// <summary>Pierwsza oferta pomocnika (jutro, 2 godziny).</summary>
    public static Offer AddHelperOffer(this Announcement announcement, UserId helperId, decimal price = 120m)
        => announcement.AddOffer(
            Id.New(),
            helperId,
            Price.Pln(price),
            Now.AddDays(1),
            Now.AddDays(1).AddHours(2),
            "Mogę wpaść po pracy.",
            Now);

    /// <summary>Kontroferta złożona przez <paramref name="actorId"/> godzinę później (pojutrze, 2 godziny).</summary>
    public static Offer Counter(this Announcement announcement, Offer offer, UserId actorId, decimal price = 100m)
        => announcement.CounterOffer(
            offer.Id,
            Id.New(),
            actorId,
            Price.Pln(price),
            Now.AddDays(2),
            Now.AddDays(2).AddHours(2),
            null,
            Now.AddHours(1));

    /// <summary>Buduje ogłoszenie w podanym statusie, przechodząc przez prawdziwe metody domenowe.</summary>
    public static Announcement InStatus(AnnouncementStatus status)
    {
        var announcement = CreateAnnouncement();

        if (status == AnnouncementStatus.Open)
        {
            return announcement;
        }

        if (status == AnnouncementStatus.Cancelled)
        {
            announcement.Cancel(OwnerId);
            return announcement;
        }

        var offer = announcement.AddHelperOffer(Helper);
        announcement.AcceptOffer(offer.Id, OwnerId, Now);
        if (status == AnnouncementStatus.Accepted)
        {
            return announcement;
        }

        announcement.Start(OwnerId);
        if (status == AnnouncementStatus.InProgress)
        {
            return announcement;
        }

        announcement.Complete(OwnerId);
        return announcement;
    }
}
