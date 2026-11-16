using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ICanHelp.Api.Tests.Infrastructure;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Notifications;

namespace ICanHelp.Api.Tests;

/// <summary>
/// Krok 14 na prawdziwym SQL Serverze: strefa alertu liczona przez trigger (STBuffer w metrach), dopasowanie
/// <c>Zone.STIntersects</c>, odległość od punktu / trasy. Augustów ~ (53.84, 22.98), Suwałki ~ (54.10, 22.93).
/// Baza jest wspólna – inne testy też tworzą ogłoszenia koło Augustowa, więc sprawdzamy powiadomienia o własnych ogłoszeniach.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AlertTests
{
    private const double AugustowLat = 53.84, AugustowLon = 22.98;
    private const double SuwalkiLat = 54.10, SuwalkiLon = 22.93;

    // AlertDto ma enumy (Kind, Schedule.Days) – API zwraca je jako tekst.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;

    public AlertTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Area_AnnouncementInRadius_Notifies_OutsideDoesNot()
    {
        var plumber = await _factory.CreateUserAsync();
        var owner = await _factory.CreateUserAsync();
        var (category, _) = await CategoriesAsync(owner.Client);

        await CreateAlertAsync(plumber, AreaBody("Dom", category.Id, AugustowLat, AugustowLon, radiusKm: 5));

        var near = await CreateAnnouncementAsync(owner, category, 53.86, 22.98, "Cieknący kran – 2 km"); // ~2,2 km
        var far = await CreateAnnouncementAsync(owner, category, 53.95, 22.98, "Cieknący kran – 12 km");  // ~12 km
        await _factory.ProcessOutboxAsync();

        var notification = (await AlertNotificationsAsync(plumber, near)).ShouldHaveSingleItem();
        notification.Parameters[NotificationPayload.AnnouncementTitle].ShouldBe("Cieknący kran – 2 km");
        notification.Parameters[NotificationPayload.CategoryName].ShouldBe(category.Name);
        notification.Parameters[NotificationPayload.AlertName].ShouldBe("Dom");
        notification.Parameters[NotificationPayload.AlertKind].ShouldBe("Area");
        Km(notification).ShouldBeInRange(1.8, 2.6);

        (await AlertNotificationsAsync(plumber, far)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Route_3KmFromTheLine_Notifies_10KmDoesNot()
    {
        var plumber = await _factory.CreateUserAsync();
        var owner = await _factory.CreateUserAsync();
        var (category, _) = await CategoriesAsync(owner.Client);

        await CreateAlertAsync(plumber, RouteBody("Suwałki → Augustów", category.Id, radiusKm: 5,
            (SuwalkiLat, SuwalkiLon), (AugustowLat, AugustowLon)));

        // Połowa drogi ~ (53.97, 22.955); na tej szerokości 1° długości ≈ 65,6 km.
        var threeKm = await CreateAnnouncementAsync(owner, category, 53.97, 22.955 + 3 / 65.6, "3 km od drogi");
        var tenKm = await CreateAnnouncementAsync(owner, category, 53.97, 22.955 + 10 / 65.6, "10 km od drogi");
        await _factory.ProcessOutboxAsync();

        var notification = (await AlertNotificationsAsync(plumber, threeKm)).ShouldHaveSingleItem();
        notification.Parameters[NotificationPayload.AlertKind].ShouldBe("Route");
        Km(notification).ShouldBeInRange(2.5, 3.5);

        (await AlertNotificationsAsync(plumber, tenKm)).ShouldBeEmpty();
    }

    [Fact]
    public async Task OtherCategory_OtherSubcategory_AndOwnAnnouncement_GiveNothing()
    {
        var plumber = await _factory.CreateUserAsync();
        var owner = await _factory.CreateUserAsync();
        var (category, other) = await CategoriesAsync(owner.Client);
        var chosen = category.Subcategories[0];
        var notChosen = category.Subcategories[1];

        await CreateAlertAsync(plumber, AreaBody("Dom", category.Id, AugustowLat, AugustowLon, radiusKm: 5,
            subcategoryIds: [chosen.Id]));

        var otherCategory = await CreateAnnouncementAsync(owner, other, AugustowLat, AugustowLon, "Inna kategoria");
        var otherSubcategory = await CreateAnnouncementAsync(owner, category, AugustowLat, AugustowLon,
            "Inna podkategoria", notChosen.Id);
        var own = await CreateAnnouncementAsync(plumber, category, AugustowLat, AugustowLon, "Moje własne", chosen.Id);
        var matching = await CreateAnnouncementAsync(owner, category, AugustowLat, AugustowLon, "Pasuje", chosen.Id);
        await _factory.ProcessOutboxAsync();

        (await AlertNotificationsAsync(plumber, otherCategory)).ShouldBeEmpty();
        (await AlertNotificationsAsync(plumber, otherSubcategory)).ShouldBeEmpty();
        (await AlertNotificationsAsync(plumber, own)).ShouldBeEmpty();
        (await AlertNotificationsAsync(plumber, matching)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task TwoMatchingAlerts_GiveOneNotification()
    {
        var plumber = await _factory.CreateUserAsync();
        var owner = await _factory.CreateUserAsync();
        var (category, _) = await CategoriesAsync(owner.Client);

        await CreateAlertAsync(plumber, AreaBody("Dom", category.Id, AugustowLat, AugustowLon, radiusKm: 10));
        await CreateAlertAsync(plumber, RouteBody("Z pracy", category.Id, radiusKm: 10,
            (SuwalkiLat, SuwalkiLon), (AugustowLat, AugustowLon)));

        var announcementId = await CreateAnnouncementAsync(owner, category, 53.87, 22.975, "W obu strefach");
        await _factory.ProcessOutboxAsync();

        (await AlertNotificationsAsync(plumber, announcementId)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Crud_Patch_RecomputesZone_Disable_Preview_AndPrivacy()
    {
        var plumber = await _factory.CreateUserAsync();
        var owner = await _factory.CreateUserAsync();
        var stranger = await _factory.CreateUserAsync();
        var (category, _) = await CategoriesAsync(owner.Client);

        var alertId = await CreateAlertAsync(plumber, AreaBody("Dom", category.Id, AugustowLat, AugustowLon,
            radiusKm: 5, schedule: new { days = "All" }));

        var alert = await plumber.Client.GetFromJsonAsync<AlertDto>($"/api/me/alerts/{alertId}", Json);
        alert.ShouldNotBeNull();
        alert.Name.ShouldBe("Dom");
        alert.Center.ShouldNotBeNull().Latitude.ShouldBe(AugustowLat);
        alert.RoutePoints.ShouldBeEmpty();
        alert.IsEnabled.ShouldBeTrue();
        (await plumber.Client.GetFromJsonAsync<List<AlertDto>>("/api/me/alerts", Json))!
            .ShouldHaveSingleItem().Id.ShouldBe(alertId);

        // Obcy: lista pusta, szczegóły / zmiana / usunięcie → 404.
        (await stranger.Client.GetFromJsonAsync<List<AlertDto>>("/api/me/alerts", Json))!.ShouldBeEmpty();
        (await stranger.Client.GetAsync($"/api/me/alerts/{alertId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await (await stranger.Client.PatchAsJsonAsync($"/api/me/alerts/{alertId}", new { radiusKm = 1 }))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "AlertNotFound");
        (await stranger.Client.DeleteAsync($"/api/me/alerts/{alertId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Przeniesienie do Suwałk: trigger przelicza strefę – ogłoszenie w Suwałkach pasuje, w Augustowie już nie.
        (await plumber.Client.PatchAsJsonAsync($"/api/me/alerts/{alertId}",
                new { center = new { latitude = SuwalkiLat, longitude = SuwalkiLon }, radiusKm = 3 }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var inSuwalki = await CreateAnnouncementAsync(owner, category, 54.11, 22.93, "W Suwałkach");
        var inAugustow = await CreateAnnouncementAsync(owner, category, AugustowLat, AugustowLon, "W Augustowie");
        await _factory.ProcessOutboxAsync();

        (await AlertNotificationsAsync(plumber, inSuwalki)).ShouldHaveSingleItem();
        (await AlertNotificationsAsync(plumber, inAugustow)).ShouldBeEmpty();

        // Podgląd: otwarte ogłoszenia w strefie teraz.
        var preview = await plumber.Client.GetFromJsonAsync<List<AnnouncementDto>>($"/api/me/alerts/{alertId}/preview");
        preview.ShouldNotBeNull();
        preview.ShouldContain(a => a.Id == inSuwalki);
        preview.ShouldNotContain(a => a.Id == inAugustow);
        (await stranger.Client.GetAsync($"/api/me/alerts/{alertId}/preview")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Wyłączony alert milczy.
        (await plumber.Client.PostEmptyAsync($"/api/me/alerts/{alertId}/disable")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);
        var whileDisabled = await CreateAnnouncementAsync(owner, category, 54.11, 22.93, "Gdy wyłączony");
        await _factory.ProcessOutboxAsync();
        (await AlertNotificationsAsync(plumber, whileDisabled)).ShouldBeEmpty();
        (await plumber.Client.GetFromJsonAsync<AlertDto>($"/api/me/alerts/{alertId}", Json))!.IsEnabled.ShouldBeFalse();

        (await plumber.Client.DeleteAsync($"/api/me/alerts/{alertId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await plumber.Client.GetAsync($"/api/me/alerts/{alertId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InvalidData_400_AndTheEleventhAlert_409()
    {
        var user = await _factory.CreateUserAsync();
        var (category, other) = await CategoriesAsync(user.Client);

        await (await user.Client.PostAsJsonAsync("/api/me/alerts",
                AreaBody("Za duży", category.Id, AugustowLat, AugustowLon, radiusKm: 100)))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "InvalidAlert");

        await (await user.Client.PostAsJsonAsync("/api/me/alerts",
                RouteBody("Jeden punkt", category.Id, 5, (SuwalkiLat, SuwalkiLon))))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "InvalidAlert");

        await (await user.Client.PostAsJsonAsync("/api/me/alerts",
                AreaBody("Cudza podkategoria", category.Id, AugustowLat, AugustowLon, 5,
                    subcategoryIds: [other.Subcategories[0].Id])))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "InvalidCategory");

        for (var i = 0; i < 10; i++)
        {
            await CreateAlertAsync(user, AreaBody($"Alert {i}", category.Id, AugustowLat, AugustowLon, 1));
        }

        await (await user.Client.PostAsJsonAsync("/api/me/alerts",
                AreaBody("Jedenasty", category.Id, AugustowLat, AugustowLon, 1)))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
    }

    [Fact]
    public async Task WithoutToken_401()
        => (await _factory.CreateClient().GetAsync("/api/me/alerts")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

    // ---------- helpers ----------

    /// <summary>Kategoria z co najmniej dwiema podkategoriami i jakaś inna.</summary>
    private static async Task<(CategoryDto Category, CategoryDto Other)> CategoriesAsync(HttpClient client)
    {
        var categories = await client.GetFromJsonAsync<List<CategoryDto>>("/api/categories");
        categories.ShouldNotBeNull();
        var category = categories.First(c => c.Subcategories.Count >= 2);
        var other = categories.First(c => c.Id != category.Id && c.Subcategories.Count > 0);
        return (category, other);
    }

    private static object AreaBody(string name, Guid categoryId, double latitude, double longitude, double radiusKm,
        Guid[]? subcategoryIds = null, object? schedule = null) => new
    {
        name,
        kind = "Area",
        categoryId,
        subcategoryIds = subcategoryIds ?? [],
        center = new { latitude, longitude },
        radiusKm,
        schedule
    };

    private static object RouteBody(string name, Guid categoryId, double radiusKm,
        params (double Latitude, double Longitude)[] points) => new
    {
        name,
        kind = "Route",
        categoryId,
        routePoints = points.Select(p => new { latitude = p.Latitude, longitude = p.Longitude }).ToArray(),
        radiusKm
    };

    private static async Task<Guid> CreateAlertAsync(TestUser user, object body)
    {
        var response = await user.Client.PostAsJsonAsync("/api/me/alerts", body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.ReadAsync<CreatedDto>()).Id;
    }

    private static async Task<Guid> CreateAnnouncementAsync(TestUser owner, CategoryDto category, double latitude,
        double longitude, string title, Guid? subcategoryId = null)
    {
        var response = await owner.Client.PostAsJsonAsync("/api/announcements", new
        {
            categoryId = category.Id,
            subcategoryId = subcategoryId ?? category.Subcategories[0].Id,
            difficulty = "Easy",
            title,
            description = "Ogłoszenie z testów alertów.",
            budget = new { amount = 100m, currency = "PLN" },
            location = new { latitude, longitude }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.ReadAsync<CreatedDto>()).Id;
    }

    private static async Task<List<NotificationDto>> AlertNotificationsAsync(TestUser user, Guid announcementId)
        => (await user.GetNotificationsAsync())
            .Where(n => n.Type == "NewAnnouncementNearby" && n.AnnouncementId == announcementId)
            .ToList();

    private static double Km(NotificationDto notification)
        => double.Parse(notification.Parameters[NotificationPayload.DistanceKm], CultureInfo.InvariantCulture);
}
