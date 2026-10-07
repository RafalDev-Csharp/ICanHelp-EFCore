using ICanHelp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Infrastructure.DAL.Configurations
{
    internal class LocationConfiguration : IEntityTypeConfiguration<Location>
    {
        public void Configure(EntityTypeBuilder<Location> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Latitude)
                .HasColumnType("float")
                .IsRequired();
            builder.Property(x => x.Longitude)
                .HasColumnType("float")
                .IsRequired();

            builder.HasOne(x => x.Announcement)
                .WithOne(x => x.Location)
                .HasForeignKey<Location>(x => x.AnnouncementId)
                .IsRequired();
        }
    }
}
