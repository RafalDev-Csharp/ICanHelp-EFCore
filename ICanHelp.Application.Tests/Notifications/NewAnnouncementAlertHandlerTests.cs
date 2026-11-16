using ICanHelp.Application.Alerts;
using ICanHelp.Application.Notifications;
using ICanHelp.Application.Tests.Fakes;
using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Alerts;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Events;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Tests.Notifications;

/// <summary>
/// Krok 14: nowe ogłoszenie → powiadomienia z alertów. Geometria jest w SQL (testy Api); tu: wybór odbiorców,
/// jedno na użytkownika, kategoria/podkategoria, harmonogram, dzienny limit + skrót, cisza nocna, idempotencja.
/// </summary>
public class NewAnnouncementAlertHandlerTests
{
    // 14.01.2026 (środa) 12:00 UTC = 13:00 w Warszawie.
    private static readonly DateTime Now = new(2026, 1, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryAnnouncementRepository _announcements = new();
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly InMemoryAnnouncementAlertRepository _alerts = new();
    private readonly InMemoryNotificationRepository _notifications = new();
    private readonly RecordingNotificationSender _sender = new();
    private readonly AlertOptions _options = new() { DailyLimit = 2 };

    private readonly UserId _owner = UserId.New();
    private readonly UserId _plumber = UserId.New();
    private readonly Category _plumbing;
    private readonly Id _leakingTap;
    private readonly Id _blockedDrain;

    public NewAnnouncementAlertHandlerTests()
    {
        _plumbing = new Category(Id.New(), "Hydraulika");
        _leakingTap = _plumbing.AddSubcategory(Id.New(), "Cieknący kran").Id;
        _blockedDrain = _plumbing.AddSubcategory(Id.New(), "Zatkany odpływ").Id;
        _categories.Categories.Add(_plumbing);
    }

    private NewAnnouncementAlertHandler CreateHandler(DateTime? nowUtc = null)
        => new(_announcements, _categories, _alerts, _notifications, new FakeUnitOfWork(), _sender,
            new FixedClock { UtcNow = nowUtc ?? Now }, _options);

    private AnnouncementAlert AddAlert(UserId userId, string name = "Dom", double distanceMeters = 1000,
        params Id[] subcategories)
    {
        var alert = new AnnouncementAlert(Id.New(), userId, name, AlertKind.Area, _plumbing.Id, subcategories,
            [new Location(53.84, 22.98)], 5, Now.AddDays(-1));
        _alerts.Alerts.Add(alert);
        _alerts.Distances[alert.Id] = distanceMeters;
        return alert;
    }

    private (Guid EventId, AnnouncementPublished Event) Publish(string title = "Cieknący kran", Id? subcategory = null,
        DateTime? at = null)
    {
        var announcement = new Announcement(Id.New(), _owner, _plumbing.Id, subcategory ?? _leakingTap,
            DifficultyLevel.Easy, title, "Kran w kuchni cieknie od tygodnia.", Price.Pln(150),
            new Location(53.85, 22.97), at ?? Now);
        _announcements.Announcements.Add(announcement);

        var published = announcement.DomainEvents.OfType<AnnouncementPublished>().Single();
        return (Guid.NewGuid(), published);
    }

    [Fact]
    public async Task MatchingAlert_NotifiesItsOwner_WithTitleCategoryAlertAndDistance()
    {
        AddAlert(_plumber, "Dom w Augustowie", distanceMeters: 3240);
        var (eventId, e) = Publish();

        await CreateHandler().HandleAsync(eventId, e);

        var notification = _notifications.Notifications.ShouldHaveSingleItem();
        notification.UserId.ShouldBe(_plumber);
        notification.Type.ShouldBe(NotificationType.NewAnnouncementNearby);
        notification.AnnouncementId!.Value.ShouldBe(e.AnnouncementId);
        notification.ConversationId.ShouldBeNull();
        notification.CreatedAt.ShouldBe(e.OccurredAt);

        var parameters = NotificationPayload.Deserialize(notification.Payload);
        parameters[NotificationPayload.AnnouncementTitle].ShouldBe("Cieknący kran");
        parameters[NotificationPayload.CategoryName].ShouldBe("Hydraulika");
        parameters[NotificationPayload.AlertName].ShouldBe("Dom w Augustowie");
        parameters[NotificationPayload.AlertKind].ShouldBe("Area");
        parameters[NotificationPayload.DistanceKm].ShouldBe("3.2");

        _sender.Sent.ShouldHaveSingleItem().UserId.ShouldBe(_plumber.Value);
    }

    [Fact]
    public async Task TwoMatchingAlertsOfOneUser_GiveOneNotification_FromTheNearest()
    {
        AddAlert(_plumber, "Dom", distanceMeters: 4000);
        AddAlert(_plumber, "Droga z pracy", distanceMeters: 1500);
        var (eventId, e) = Publish();

        await CreateHandler().HandleAsync(eventId, e);

        var notification = _notifications.Notifications.ShouldHaveSingleItem();
        NotificationPayload.Deserialize(notification.Payload)[NotificationPayload.AlertName].ShouldBe("Droga z pracy");
    }

    [Fact]
    public async Task OwnAlert_OfTheAnnouncementOwner_GivesNothing()
    {
        AddAlert(_owner);
        var (eventId, e) = Publish();

        await CreateHandler().HandleAsync(eventId, e);

        _notifications.Notifications.ShouldBeEmpty();
        _sender.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task OtherSubcategory_GivesNothing_WholeCategory_Matches()
    {
        var onlyDrains = UserId.New();
        AddAlert(onlyDrains, "Tylko odpływy", 1000, _blockedDrain);
        AddAlert(_plumber);
        var (eventId, e) = Publish(subcategory: _leakingTap);

        await CreateHandler().HandleAsync(eventId, e);

        _notifications.Notifications.ShouldHaveSingleItem().UserId.ShouldBe(_plumber);
    }

    [Fact]
    public async Task DisabledAlert_GivesNothing()
    {
        AddAlert(_plumber).Disable(_plumber, Now);
        var (eventId, e) = Publish();

        await CreateHandler().HandleAsync(eventId, e);

        _notifications.Notifications.ShouldBeEmpty();
    }

    [Fact]
    public async Task Schedule_IsCheckedInLocalTime()
    {
        // 13:00 w Warszawie: alert 15–18 milczy, alert 12–14 działa.
        AddAlert(_plumber).SetSchedule(_plumber, WeekDays.All, new TimeOnly(15, 0), new TimeOnly(18, 0), Now);
        var lunch = UserId.New();
        AddAlert(lunch).SetSchedule(lunch, WeekDays.Wednesday, new TimeOnly(12, 0), new TimeOnly(14, 0), Now);
        var (eventId, e) = Publish();

        await CreateHandler().HandleAsync(eventId, e);

        _notifications.Notifications.ShouldHaveSingleItem().UserId.ShouldBe(lunch);
    }

    [Fact]
    public async Task SameEventTwice_GivesOneNotification()
    {
        AddAlert(_plumber);
        var (eventId, e) = Publish();
        var handler = CreateHandler();

        await handler.HandleAsync(eventId, e);
        await handler.HandleAsync(eventId, e);

        _notifications.Notifications.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AnnouncementNoLongerOpen_GivesNothing()
    {
        AddAlert(_plumber);
        var (eventId, e) = Publish();
        _announcements.Announcements.Single().Cancel(_owner);

        await CreateHandler().HandleAsync(eventId, e);

        _notifications.Notifications.ShouldBeEmpty();
    }

    [Fact]
    public async Task OverTheDailyLimit_OneDigestIsUpdated_InsteadOfNewNotifications()
    {
        AddAlert(_plumber);
        var handler = CreateHandler();

        // Limit = 2, potem skrót: 1, 2.
        for (var i = 0; i < 4; i++)
        {
            var (eventId, e) = Publish($"Ogłoszenie {i}", at: Now.AddMinutes(i));
            await handler.HandleAsync(eventId, e);
        }

        _notifications.Notifications.Count(n => n.Type == NotificationType.NewAnnouncementNearby).ShouldBe(2);
        var digest = _notifications.Notifications
            .Where(n => n.Type == NotificationType.NewAnnouncementsDigest)
            .ShouldHaveSingleItem();
        NotificationPayload.Deserialize(digest.Payload)[NotificationPayload.Count].ShouldBe("2");
        digest.CreatedAt.ShouldBe(Now.AddMinutes(3));
        digest.ReadAt.ShouldBeNull();

        // Skrót też idzie na żywo (przy każdej zmianie).
        _sender.Sent.Count.ShouldBe(4);
    }

    [Fact]
    public async Task DailyLimit_StartsAgainTheNextLocalDay()
    {
        AddAlert(_plumber);
        var handler = CreateHandler();

        foreach (var at in new[] { Now, Now.AddMinutes(1), Now.AddDays(1) })
        {
            var (eventId, e) = Publish(at: at);
            await handler.HandleAsync(eventId, e);
        }

        _notifications.Notifications.Count(n => n.Type == NotificationType.NewAnnouncementNearby).ShouldBe(3);
        _notifications.Notifications.ShouldNotContain(n => n.Type == NotificationType.NewAnnouncementsDigest);
    }

    [Fact]
    public async Task QuietHours_NotificationIsSaved_ButNotDeliveredLive()
    {
        AddAlert(_plumber);
        var (eventId, e) = Publish();

        // 22:30 UTC = 23:30 w Warszawie (zima).
        await CreateHandler(nowUtc: new DateTime(2026, 1, 14, 22, 30, 0, DateTimeKind.Utc)).HandleAsync(eventId, e);

        _notifications.Notifications.ShouldHaveSingleItem();
        _sender.Sent.ShouldBeEmpty();
    }
}
