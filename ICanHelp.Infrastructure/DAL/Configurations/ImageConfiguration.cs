using ICanHelp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ICanHelp.Infrastructure.DAL.Configurations;

internal sealed class ImageConfiguration : IEntityTypeConfiguration<Image>
{
    public void Configure(EntityTypeBuilder<Image> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.FileName).IsRequired();
        builder.Property(x => x.FileExtension).IsRequired().HasMaxLength(10);
        builder.Property(x => x.FilePath).IsRequired();
        builder.Property(x => x.AddedAt).IsRequired();

        // The relation to Announcement is configured in AnnouncementConfiguration (the aggregate root).
    }
}
