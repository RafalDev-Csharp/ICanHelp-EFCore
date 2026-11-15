using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Repositories;

internal sealed class RefreshTokenRepository(ICanHelpDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveAsync(UserId userId, DateTime nowUtc,
        CancellationToken cancellationToken = default)
        => await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > nowUtc)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
        => await db.RefreshTokens.AddAsync(refreshToken, cancellationToken);
}
