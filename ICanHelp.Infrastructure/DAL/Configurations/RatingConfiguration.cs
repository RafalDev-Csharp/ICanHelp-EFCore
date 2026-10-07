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
    internal class RatingConfiguration : IEntityTypeConfiguration<Rating>
    {
        public void Configure(EntityTypeBuilder<Rating> builder)
        {
            builder.HasKey(r => r.Id);

            builder.Property(x => x.Rate)
                .HasConversion(
                    rate => rate.Value,
                    value => new Rate(value))
                .IsRequired();
            builder.HasOne(r => r.Announcement)
                .WithMany(a => a.Ratings)
                .HasForeignKey(r => r.AnnouncementId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(r => r.User)
                .WithMany(u => u.ClientRatings)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(r => r.User)
                .WithMany(u => u.HelperRatings)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(r => r.RatedUser)
                .WithMany(u => u.ClientRatings)
                .HasForeignKey(r => r.RatedUserId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(r => r.RatedUser)
                .WithMany(u => u.HelperRatings)
                .HasForeignKey(r => r.RatedUserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
