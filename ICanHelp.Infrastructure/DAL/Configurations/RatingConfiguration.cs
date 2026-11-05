using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ICanHelp.Infrastructure.DAL.Configurations;

internal sealed class RatingConfiguration : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Rate).IsRequired();
        builder.Property(x => x.Comment).IsRequired(false);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasOne<Announcement>()
            .WithMany()
            .HasForeignKey(x => x.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);

        // Two relations to the same table: SQL Server does not allow two cascade paths, so both are Restrict.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.RaterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.RateeId)
            .OnDelete(DeleteBehavior.Restrict);

        // One rater can rate an announcement only once.
        builder.HasIndex(x => new { x.AnnouncementId, x.RaterId }).IsUnique();
        builder.HasIndex(x => x.RateeId);
    }
}
