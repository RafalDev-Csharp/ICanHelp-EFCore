using ICanHelp.Application.DTO;
using ICanHelp.Application.Notifications;
using ICanHelp.Application.Security;
using ICanHelp.Application.Storage;
using ICanHelp.Application.Time;
using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Alerts;
using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Entities.Notifications;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Tests.Fakes;

// Proste ręczne zaślepki – bez bibliotek do mockowania.

internal sealed class InMemoryUserRepository : IUserRepository
{
    public List<User> Users { get; } = new();

    public Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default)
        => Task.FromResult(Users.SingleOrDefault(u => u.Id == id));

    public Task<User?> GetByUserNameAsync(UserName userName, CancellationToken cancellationToken = default)
        => Task.FromResult(Users.SingleOrDefault(u => u.UserName == userName));

    public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
        => Task.FromResult(Users.SingleOrDefault(u => u.ContactData.Email == email));

    public Task<bool> ExistsByUserNameAsync(UserName userName, CancellationToken cancellationToken = default)
        => Task.FromResult(Users.Any(u => u.UserName == userName));

    public Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default)
        => Task.FromResult(Users.Any(u => u.ContactData.Email == email));

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        Users.Add(user);
        return Task.CompletedTask;
    }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Executions { get; private set; }

    public async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        Executions++;
        await action();
    }
}

/// <summary>"Hash" = "hashed:" + hasło – wystarczy, żeby sprawdzić, że handler nie zapisuje hasła wprost.</summary>
internal sealed class FakePasswordManager : IPasswordManager
{
    public const string Prefix = "hashed:";

    public string Secure(string password) => Prefix + password;

    public bool Validate(string password, string passwordHash) => passwordHash == Prefix + password;
}

internal sealed class FixedClock : IClock
{
    public DateTime UtcNow { get; init; } = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
}

internal sealed class FakeAuthenticator : IAuthenticator
{
    public (Guid UserId, string UserName, UserRole Role)? LastCall { get; private set; }

    public JwtDto CreateToken(Guid userId, string userName, UserRole role)
    {
        LastCall = (userId, userName, role);
        return new JwtDto { AccessToken = $"token-for-{userId}", ExpiresAtUtc = DateTime.UtcNow.AddHours(1) };
    }
}

internal sealed class FakeTokenStorage : ITokenStorage
{
    private JwtDto? _jwt;

    public void Set(JwtDto jwt) => _jwt = jwt;

    public JwtDto? Get() => _jwt;
}

internal sealed class InMemoryAnnouncementRepository : IAnnouncementRepository
{
    public List<Announcement> Announcements { get; } = new();

    public Task<Announcement?> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
        => Task.FromResult(Announcements.SingleOrDefault(a => a.Id == id));

    public Task AddAsync(Announcement announcement, CancellationToken cancellationToken = default)
    {
        Announcements.Add(announcement);
        return Task.CompletedTask;
    }

    public void Remove(Announcement announcement) => Announcements.Remove(announcement);
}

internal sealed class InMemoryLikedAnnouncementRepository : ILikedAnnouncementRepository
{
    public List<LikedAnnouncement> Likes { get; } = new();

    public Task<LikedAnnouncement?> GetAsync(UserId userId, Id announcementId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Likes.SingleOrDefault(l => l.UserId == userId && l.AnnouncementId == announcementId));

    public Task AddAsync(LikedAnnouncement likedAnnouncement, CancellationToken cancellationToken = default)
    {
        Likes.Add(likedAnnouncement);
        return Task.CompletedTask;
    }

    public void Remove(LikedAnnouncement likedAnnouncement) => Likes.Remove(likedAnnouncement);
}

internal sealed class InMemoryCategoryRepository : ICategoryRepository
{
    public List<Category> Categories { get; } = new();

    public Task<Category?> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
        => Task.FromResult(Categories.SingleOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Category>>(Categories);

    public Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        Categories.Add(category);
        return Task.CompletedTask;
    }

    public void Remove(Category category) => Categories.Remove(category);
}

internal sealed class InMemoryRatingRepository : IRatingRepository
{
    public List<Rating> Ratings { get; } = new();

    public Task<Rating?> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
        => Task.FromResult(Ratings.SingleOrDefault(r => r.Id == id));

    public Task<bool> ExistsAsync(Id announcementId, UserId raterId, CancellationToken cancellationToken = default)
        => Task.FromResult(Ratings.Any(r => r.AnnouncementId == announcementId && r.RaterId == raterId));

    public Task AddAsync(Rating rating, CancellationToken cancellationToken = default)
    {
        Ratings.Add(rating);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryConversationRepository : IConversationRepository
{
    public List<Conversation> Conversations { get; } = new();

    public Task<Conversation?> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
        => Task.FromResult(Conversations.SingleOrDefault(c => c.Id == id));

    public Task<Conversation?> GetByParticipantsAsync(Id announcementId, UserId helperId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Conversations.SingleOrDefault(c => c.AnnouncementId == announcementId && c.HelperId == helperId));

    public Task<IReadOnlyList<(Id ConversationId, UserId HelperId)>> GetParticipantsAsync(Id announcementId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<(Id ConversationId, UserId HelperId)>>(Conversations
            .Where(c => c.AnnouncementId == announcementId)
            .Select(c => (c.Id, c.HelperId))
            .ToList());

    public Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        Conversations.Add(conversation);
        return Task.CompletedTask;
    }
}

/// <summary>Pliki w słowniku: ścieżka → bajty.</summary>
internal sealed class InMemoryFileStorage : IFileStorage
{
    public Dictionary<string, byte[]> Files { get; } = new();

    public async Task SaveAsync(string path, Stream content, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        Files[path] = buffer.ToArray();
    }

    public Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult<Stream?>(Files.TryGetValue(path, out var bytes) ? new MemoryStream(bytes) : null);

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        Files.Remove(path);
        return Task.CompletedTask;
    }
}

/// <summary>Jak <see cref="FakeUnitOfWork"/>, ale „zapis do bazy” po akcji się nie udaje (np. konflikt współbieżności).</summary>
internal sealed class FailingUnitOfWork : IUnitOfWork
{
    public async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        await action();
        throw new InvalidOperationException("SaveChanges failed.");
    }
}

internal sealed class InMemoryNotificationRepository : INotificationRepository
{
    public List<Notification> Notifications { get; } = new();

    public Task<Notification?> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
        => Task.FromResult(Notifications.SingleOrDefault(n => n.Id == id));

    public Task<IReadOnlyList<Notification>> GetUnreadAsync(UserId userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Notification>>(
            Notifications.Where(n => n.UserId == userId && n.ReadAt == null).ToList());

    public Task<bool> ExistsAsync(UserId userId, Guid sourceEventId, CancellationToken cancellationToken = default)
        => Task.FromResult(Notifications.Any(n => n.UserId == userId && n.SourceEventId == sourceEventId));

    public Task<int> CountSinceAsync(UserId userId, NotificationType type, DateTime sinceUtc,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Notifications.Count(n => n.UserId == userId && n.Type == type && n.CreatedAt >= sinceUtc));

    public Task<Notification?> GetLatestSinceAsync(UserId userId, NotificationType type, DateTime sinceUtc,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Notifications
            .Where(n => n.UserId == userId && n.Type == type && n.CreatedAt >= sinceUtc)
            .OrderByDescending(n => n.CreatedAt)
            .FirstOrDefault());

    public Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        Notifications.Add(notification);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Alerty w liście. Geometrii nie ma (to robi SQL Server) – kandydatami są włączone alerty kategorii bez alertów
/// właściciela, a odległość podaje test (<see cref="Distances"/>, domyślnie 1 km).
/// </summary>
internal sealed class InMemoryAnnouncementAlertRepository : IAnnouncementAlertRepository
{
    public List<AnnouncementAlert> Alerts { get; } = new();

    /// <summary>„Odległość od kształtu” alertu w metrach; brak wpisu = 1000 m. Alert z <c>double.NaN</c> = poza strefą.</summary>
    public Dictionary<Id, double> Distances { get; } = new();

    public Task<AnnouncementAlert?> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
        => Task.FromResult(Alerts.SingleOrDefault(a => a.Id == id));

    public Task<int> CountByUserAsync(UserId userId, CancellationToken cancellationToken = default)
        => Task.FromResult(Alerts.Count(a => a.UserId == userId));

    public Task<IReadOnlyList<AlertCandidate>> FindCandidatesAsync(Id categoryId, UserId excludedUserId,
        Location point, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AlertCandidate>>(Alerts
            .Where(a => a.IsEnabled && a.CategoryId == categoryId && a.UserId != excludedUserId)
            .Select(a => new AlertCandidate(a, Distances.TryGetValue(a.Id, out var d) ? d : 1000))
            .Where(c => !double.IsNaN(c.DistanceMeters))
            .ToList());

    public Task AddAsync(AnnouncementAlert alert, CancellationToken cancellationToken = default)
    {
        Alerts.Add(alert);
        return Task.CompletedTask;
    }

    public void Remove(AnnouncementAlert alert) => Alerts.Remove(alert);
}

/// <summary>Zapamiętuje, co i do kogo „wysłano” (kanał SignalR w testach Application).</summary>
internal sealed class RecordingNotificationSender : INotificationSender
{
    public List<(Guid UserId, NotificationDto Notification)> Sent { get; } = new();

    public Task SendAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default)
    {
        Sent.Add((userId, notification));
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
{
    public List<RefreshToken> Tokens { get; } = new();

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => Task.FromResult(Tokens.SingleOrDefault(t => t.TokenHash == tokenHash));

    public Task<IReadOnlyList<RefreshToken>> GetActiveAsync(UserId userId, DateTime nowUtc,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RefreshToken>>(
            Tokens.Where(t => t.UserId == userId && t.IsActive(nowUtc)).ToList());

    public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        Tokens.Add(refreshToken);
        return Task.CompletedTask;
    }
}

/// <summary>Kolejne tokeny "refresh-1", "refresh-2"…; hash = "h:" + token.</summary>
internal sealed class FakeRefreshTokenService : IRefreshTokenService
{
    private int _counter;

    public TimeSpan Lifetime => TimeSpan.FromDays(30);

    public GeneratedRefreshToken Generate()
    {
        var token = $"refresh-{++_counter}";
        return new GeneratedRefreshToken(token, Hash(token));
    }

    public string Hash(string token) => "h:" + token;
}

internal sealed class InMemoryUserDeviceRepository : IUserDeviceRepository
{
    public List<UserDevice> Devices { get; } = new();

    public Task<UserDevice?> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
        => Task.FromResult(Devices.SingleOrDefault(d => d.Id == id));

    public Task<UserDevice?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
        => Task.FromResult(Devices.SingleOrDefault(d => d.Token == token));

    public Task<IReadOnlyList<UserDevice>> GetByUserAsync(UserId userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<UserDevice>>(Devices.Where(d => d.UserId == userId).ToList());

    public Task AddAsync(UserDevice device, CancellationToken cancellationToken = default)
    {
        Devices.Add(device);
        return Task.CompletedTask;
    }

    public void Remove(UserDevice device) => Devices.Remove(device);
}

/// <summary>Zapamiętuje wysyłki push; <see cref="InvalidTokens"/> – tokeny, które „FCM odrzuci”.</summary>
internal sealed class RecordingPushSender : IPushSender
{
    public List<(IReadOnlyCollection<string> Tokens, PushMessage Message)> Sent { get; } = new();
    public HashSet<string> InvalidTokens { get; } = new();

    public Task<PushSendResult> SendAsync(IReadOnlyCollection<string> tokens, PushMessage message,
        CancellationToken cancellationToken = default)
    {
        Sent.Add((tokens.ToList(), message));
        return Task.FromResult(new PushSendResult(tokens.Where(InvalidTokens.Contains).ToList()));
    }
}

/// <summary>Kanał, który zapamiętuje wysyłki albo zawsze rzuca wyjątek (awaria kanału).</summary>
internal sealed class RecordingChannel : INotificationChannel
{
    public bool Fail { get; init; }
    public List<(Guid UserId, NotificationDto Notification)> Sent { get; } = new();

    public Task SendAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default)
    {
        if (Fail)
        {
            throw new InvalidOperationException("Channel is down.");
        }

        Sent.Add((userId, notification));
        return Task.CompletedTask;
    }
}

/// <summary>Zegar, który można przesuwać w teście.</summary>
internal sealed class MutableClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
}
