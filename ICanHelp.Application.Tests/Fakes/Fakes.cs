using ICanHelp.Application.DTO;
using ICanHelp.Application.Security;
using ICanHelp.Application.Time;
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
