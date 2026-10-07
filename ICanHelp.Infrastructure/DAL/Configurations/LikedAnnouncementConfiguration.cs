using ICanHelp.Core.Entities.Liked;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Infrastructure.DAL.Configurations
{
    internal class LikedAnnouncementConfiguration : IEntityTypeConfiguration<LikedAnnouncement>
    {
        public void Configure(EntityTypeBuilder<LikedAnnouncement> builder)
        {
            builder.HasKey(x => x.Id);

            builder.HasOne(x => x.Announcement)
                .WithOne(x => x.LikedAnnouncement)
                .HasForeignKey<LikedAnnouncement>(x => x.AnnouncementId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Property(x => x.IsLiked)
                .IsRequired();

            builder.HasOne(x => x.UserLiker)
                .WithMany(x => x.LikedAnnouncements)
                .HasForeignKey(x => x.AnnouncementId)
                .OnDelete(DeleteBehavior.Cascade);


        }
    }
}