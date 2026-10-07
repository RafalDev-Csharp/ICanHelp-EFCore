using ICanHelp.Core.Entities;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Infrastructure.DAL.Configurations
{
    internal class ImageConfiguration : IEntityTypeConfiguration<Image>
    {
        public void Configure(EntityTypeBuilder<Image> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.FileName)
                .HasConversion(x => x.Value, x => new StringObject(x))
                .IsRequired()
                .HasMaxLength(200);
            builder.Property(x => x.FileExtension)
                .HasConversion(x => x.Value, x => new StringObject(x))
                .IsRequired()
                .HasMaxLength(10);
            builder.Property(x => x.FilePath)
                .HasConversion(x => x.Value, x => new StringObject(x))
                .IsRequired()
                .HasMaxLength(500);

            builder.HasOne(x => x.Announcement)
                .WithMany(x => x.Images)
                .HasForeignKey(x => x.AnnouncementId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
