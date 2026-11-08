using System.Net.Http.Headers;
using ICanHelp.Application.DTO;
using ICanHelp.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace ICanHelp.Api.Tests.Infrastructure;

/// <summary>Zarejestrowany i zalogowany użytkownik testowy. <see cref="Client"/> ma już nagłówek Bearer.</summary>
public sealed record TestUser(Guid Id, string UserName, string Email, string Password, HttpClient Client);

/// <summary>Treść błędu (ProblemDetails + nasze rozszerzenia errorCode/traceId).</summary>
public sealed record Problem(int? Status, string? Title, string? Detail, string? ErrorCode, string? TraceId);

/// <summary>Kroki powtarzane w wielu testach – przez prawdziwe endpointy, bez zaglądania do bazy.</summary>
public static class TestApi
{
    public const string Password = "Haslo123!";

    public static async Task<TestUser> CreateUserAsync(this ApiFactory factory)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var userName = $"user{suffix}";
        var email = $"{userName}@example.com";

        var client = factory.CreateClient();

        var signUp = await client.PostAsJsonAsync("/api/users/sign-up", SignUpBody(userName, email));
        signUp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var userId = signUp.IdFromLocation();

        var token = await client.SignInAsync(email, Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return new TestUser(userId, userName, email, Password, client);
    }

    public static object SignUpBody(string userName, string email) => new
    {
        userName,
        fullName = "Test User",
        email,
        password = Password,
        phoneNumber = "+48600100200",
        country = "Polska",
        city = "Kraków",
        postalCode = "30-001",
        description = "Konto z testów integracyjnych."
    };

    public static async Task<string> SignInAsync(this HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/users/sign-in", new { email, password });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var jwt = await response.ReadAsync<JwtDto>();
        jwt.AccessToken.ShouldNotBeNullOrWhiteSpace();
        return jwt.AccessToken;
    }

    /// <summary>Pierwsza kategoria i podkategoria z seeda (DbInitializer).</summary>
    public static async Task<(Guid CategoryId, Guid SubcategoryId)> GetAnySubcategoryAsync(this HttpClient client)
    {
        var categories = await client.GetFromJsonAsync<List<CategoryDto>>("/api/categories");
        var category = categories!.First(c => c.Subcategories.Count > 0);
        return (category.Id, category.Subcategories.First().Id);
    }

    public static async Task<Guid> CreateAnnouncementAsync(this TestUser owner,
        double latitude = 50.0614, double longitude = 19.9366, string title = "Cieknący kran w kuchni")
    {
        var (categoryId, subcategoryId) = await owner.Client.GetAnySubcategoryAsync();

        var response = await owner.Client.PostAsJsonAsync("/api/announcements", new
        {
            categoryId,
            subcategoryId,
            difficulty = "Easy",
            title,
            description = "Kran cieknie od tygodnia, mam nową uszczelkę.",
            budget = new { amount = 150m, currency = "PLN" },
            location = new { latitude, longitude }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.ReadAsync<CreatedDto>()).Id;
    }

    /// <summary>Oferta albo kontroferta z terminem za tydzień (żeby test nie „przeterminował się” z czasem).</summary>
    public static object OfferBody(decimal amount, string? message = null)
    {
        var start = DateTime.UtcNow.Date.AddDays(7).AddHours(16);
        return new
        {
            price = new { amount, currency = "PLN" },
            proposedStart = start,
            proposedEnd = start.AddHours(2),
            message
        };
    }

    public static async Task<Guid> MakeOfferAsync(this TestUser helper, Guid announcementId, decimal amount)
    {
        var response = await helper.Client.PostAsJsonAsync(
            $"/api/announcements/{announcementId}/offers", OfferBody(amount, "Mogę wpaść po pracy."));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.ReadAsync<CreatedDto>()).Id;
    }

    /// <summary>
    /// Przetwarza wszystko, co czeka w outboksie (zdarzenia → powiadomienia) – synchronicznie, zamiast czekać na pętlę
    /// w tle (w testach wyłączona). Przetwarza też zdarzenia innych testów – nieszkodliwe, każdy ma swoich użytkowników.
    /// </summary>
    public static Task<int> ProcessOutboxAsync(this ApiFactory factory)
        => factory.Services.GetRequiredService<IOutboxProcessor>().ProcessPendingAsync();

    public static async Task<List<NotificationDto>> GetNotificationsAsync(this TestUser user, bool unreadOnly = false)
    {
        var notifications = await user.Client.GetFromJsonAsync<List<NotificationDto>>(
            $"/api/me/notifications?unreadOnly={unreadOnly}&pageSize=100");
        notifications.ShouldNotBeNull();
        return notifications;
    }

    public static async Task<int> GetUnreadNotificationCountAsync(this TestUser user)
        => (await user.Client.GetFromJsonAsync<UnreadCountDto>("/api/me/notifications/unread-count"))!.Count;

    /// <summary>POST bez body (accept/reject/withdraw/start/complete/cancel).</summary>
    public static Task<HttpResponseMessage> PostEmptyAsync(this HttpClient client, string url)
        => client.PostAsync(url, content: null);

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) where T : class
    {
        var value = await response.Content.ReadFromJsonAsync<T>();
        value.ShouldNotBeNull();
        return value;
    }

    public static async Task ShouldBeProblemAsync(this HttpResponseMessage response, HttpStatusCode status,
        string? errorCode = null)
    {
        response.StatusCode.ShouldBe(status);
        (response.Content.Headers.ContentType?.MediaType).ShouldBe("application/problem+json");

        var problem = await response.ReadAsync<Problem>();
        problem.Status.ShouldBe((int)status);
        problem.TraceId.ShouldNotBeNullOrWhiteSpace();

        if (errorCode is not null)
        {
            problem.ErrorCode.ShouldBe(errorCode);
        }
    }

    /// <summary>Id z nagłówka Location (np. http://localhost/api/users/{id}).</summary>
    public static Guid IdFromLocation(this HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        location.ShouldNotBeNull();
        return Guid.Parse(location.ToString().TrimEnd('/').Split('/')[^1]);
    }
}
