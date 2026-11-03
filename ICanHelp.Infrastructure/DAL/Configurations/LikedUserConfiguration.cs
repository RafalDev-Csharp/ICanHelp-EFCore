using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ICanHelp.Infrastructure.DAL.Configurations;

internal sealed class LikedUserConfiguration : IEntityTypeConfiguration<LikedUser>
{
    public void Configure(EntityTypeBuilder<LikedUser> builder)
    {
        builder.HasKey(x => new { x.LikerId, x.LikedId });

        builder.Property(x => x.LikedAt).IsRequired();

        // Both keys point to Users. Only one of them may cascade (SQL Server: no multiple cascade paths).
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.LikerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.LikedId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.LikedId);
    }
}
