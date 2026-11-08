using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Not revoked and not expired tokens of the user (to revoke them all).</summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveAsync(UserId userId, DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
}
