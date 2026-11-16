using ICanHelp.Core.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ICanHelp.Infrastructure.DAL.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.TokenHash).IsRequired().HasMaxLength(RefreshToken.TokenHashLength);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.ReplacedByTokenId).IsRequired(false);

        // Two requests with the same token at once: only the first rotation is saved (UPDATE ... WHERE RevokedAt IS NULL),
        // the second one ends with ConcurrencyConflict.
        builder.Property(x => x.RevokedAt).IsRequired(false).IsConcurrencyToken();

        builder.Ignore(x => x.IsRevoked);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TokenHash).IsUnique();

        // "Revoke all sessions of the user".
        builder.HasIndex(x => new { x.UserId, x.RevokedAt });
    }
}
