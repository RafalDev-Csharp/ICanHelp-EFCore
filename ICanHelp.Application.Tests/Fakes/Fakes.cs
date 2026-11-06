using ICanHelp.Application.DTO;
using ICanHelp.Application.Security;
using ICanHelp.Application.Storage;
using ICanHelp.Application.Time;
using ICanHelp.Core.Entities;
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
