using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ICanHelp.Infrastructure.DAL.Configurations;

internal sealed class LikedAnnouncementConfiguration : IEntityTypeConfiguration<LikedAnnouncement>
{
    public void Configure(EntityTypeBuilder<LikedAnnouncement> builder)
    {
        // Composite key = a user can like an announcement only once.
        builder.HasKey(x => new { x.UserId, x.AnnouncementId });

        builder.Property(x => x.LikedAt).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Announcement>()
            .WithMany()
            .HasForeignKey(x => x.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
