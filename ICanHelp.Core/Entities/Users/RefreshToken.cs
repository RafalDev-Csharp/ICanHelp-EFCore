using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities.Users;

/// <summary>
/// Aggregate root: a long-lived token that gets a new access token (JWT) without the password (Krok F2).
/// Only the hash of the token is stored. Every use rotates it: the old one is revoked and points to the new one
/// (<see cref="ReplacedByTokenId"/>), so a second use of an old token means it leaked.
/// </summary>
public class RefreshToken
{
    /// <summary>SHA-256 as hex.</summary>
    public const int TokenHashLength = 64;

    public Id Id { get; private set; }
    public UserId UserId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    /// <summary>When it was used (rotated) or the user signed out (null = not revoked).</summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>The token issued in place of this one (null = revoked by sign-out or not revoked).</summary>
    public Id? ReplacedByTokenId { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    // Required by EF Core.
    private RefreshToken()
    {
    }

    public RefreshToken(Id id, UserId userId, string tokenHash, DateTime createdAtUtc, DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length > TokenHashLength)
        {
            throw new DomainRuleException($"Refresh token hash must have 1–{TokenHashLength} characters.");
        }

        if (expiresAtUtc <= createdAtUtc)
        {
            throw new DomainRuleException("Refresh token must expire after it is created.");
        }

        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAtUtc;
        ExpiresAt = expiresAtUtc;
    }

    /// <summary>Not revoked and not expired.</summary>
    public bool IsActive(DateTime nowUtc) => RevokedAt is null && nowUtc < ExpiresAt;

    /// <summary>The token was used: it is revoked and replaced by <paramref name="newTokenId"/>.</summary>
    public void Rotate(Id newTokenId, DateTime nowUtc)
    {
        if (!IsActive(nowUtc))
        {
            throw new DomainRuleException("Only an active refresh token can be used.");
        }

        if (newTokenId == Id)
        {
            throw new DomainRuleException("Refresh token cannot replace itself.");
        }

        RevokedAt = nowUtc;
        ReplacedByTokenId = newTokenId;
    }

    /// <summary>Sign-out or a suspected leak. Revoking twice keeps the first time.</summary>
    public void Revoke(DateTime nowUtc) => RevokedAt ??= nowUtc;
}
