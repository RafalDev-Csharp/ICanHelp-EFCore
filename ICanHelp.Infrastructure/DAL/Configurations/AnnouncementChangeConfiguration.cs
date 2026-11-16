using ICanHelp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ICanHelp.Infrastructure.DAL.Configurations;

internal sealed class AnnouncementChangeConfiguration : IEntityTypeConfiguration<AnnouncementChange>
{
    public void Configure(EntityTypeBuilder<AnnouncementChange> builder)
    {
        // One entry per revision of an announcement – the pair is unique, so it is the key (no extra Id needed).
        builder.HasKey(x => new { x.AnnouncementId, x.Revision });
        builder.Property(x => x.Revision).ValueGeneratedNever();

        builder.Property(x => x.ChangedAt).IsRequired();
        builder.Property(x => x.ChangedFields).IsRequired();

        // Before/after values of each changed field – own table, one row per field per revision.
        builder.OwnsMany(x => x.Fields, field =>
        {
            field.ToTable("AnnouncementFieldChanges");
            field.WithOwner().HasForeignKey(nameof(AnnouncementChange.AnnouncementId), nameof(AnnouncementChange.Revision));
            field.HasKey(nameof(AnnouncementChange.AnnouncementId), nameof(AnnouncementChange.Revision),
                nameof(AnnouncementFieldChange.Field));
            field.Property(f => f.Field).HasMaxLength(20);
            // Longest value is a description (max 500 chars).
            field.Property(f => f.OldValue).HasMaxLength(500);
            field.Property(f => f.NewValue).HasMaxLength(500);
        });
        builder.Navigation(x => x.Fields).UsePropertyAccessMode(PropertyAccessMode.Field);

        // The relation to Announcement is configured in AnnouncementConfiguration (the aggregate root).
    }
}
