using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Tests.Entities;

/// <summary>Krok F2: refresh token – ważność, rotacja (jednorazowość) i unieważnienie.</summary>
public class RefreshTokenTests
{
    private static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string Hash = new('a', RefreshToken.TokenHashLength);

    private static RefreshToken Create(int days = 30)
        => new(Id.New(), UserId.New(), Hash, Now, Now.AddDays(days));

    [Fact]
    public void NewToken_IsActiveUntilExpiry()
    {
        var token = Create();

        token.IsActive(Now).ShouldBeTrue();
        token.IsActive(Now.AddDays(30).AddSeconds(-1)).ShouldBeTrue();
        token.IsActive(Now.AddDays(30)).ShouldBeFalse();
        token.IsRevoked.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WithoutHash_Throws(string hash)
        => Should.Throw<DomainRuleException>(() => new RefreshToken(Id.New(), UserId.New(), hash, Now, Now.AddDays(1)));

    [Fact]
    public void Create_WithTooLongHash_Throws()
        => Should.Throw<DomainRuleException>(() => new RefreshToken(Id.New(), UserId.New(),
            new string('a', RefreshToken.TokenHashLength + 1), Now, Now.AddDays(1)));

    [Fact]
    public void Create_ExpiringBeforeCreation_Throws()
        => Should.Throw<DomainRuleException>(() => new RefreshToken(Id.New(), UserId.New(), Hash, Now, Now));

    [Fact]
    public void Rotate_RevokesAndPointsToNewToken()
    {
        var token = Create();
        var newId = Id.New();

        token.Rotate(newId, Now.AddDays(1));

        token.IsRevoked.ShouldBeTrue();
        token.RevokedAt.ShouldBe(Now.AddDays(1));
        token.ReplacedByTokenId.ShouldBe(newId);
        token.IsActive(Now.AddDays(1)).ShouldBeFalse();
    }

    [Fact]
    public void Rotate_Twice_Throws()
    {
        var token = Create();
        token.Rotate(Id.New(), Now);

        Should.Throw<DomainRuleException>(() => token.Rotate(Id.New(), Now.AddMinutes(1)));
    }

    [Fact]
    public void Rotate_Expired_Throws()
        => Should.Throw<DomainRuleException>(() => Create(days: 1).Rotate(Id.New(), Now.AddDays(2)));

    [Fact]
    public void Rotate_ToItself_Throws()
    {
        var token = Create();

        Should.Throw<DomainRuleException>(() => token.Rotate(token.Id, Now));
    }

    [Fact]
    public void Revoke_KeepsFirstTime_AndHasNoReplacement()
    {
        var token = Create();

        token.Revoke(Now.AddHours(1));
        token.Revoke(Now.AddHours(2));

        token.RevokedAt.ShouldBe(Now.AddHours(1));
        token.ReplacedByTokenId.ShouldBeNull();
        token.IsActive(Now.AddHours(1)).ShouldBeFalse();
    }
}
