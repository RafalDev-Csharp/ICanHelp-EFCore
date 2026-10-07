using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Entities.Users;
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
    internal class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .HasConversion(x => x.Value, x => new UserId(x));
            builder.Property(x => x.UserName)
                .HasConversion(x => x.Value, x => new UserName(x));
            builder.Property(x => x.Password)
            .HasConversion(x => x.Value, x => new(x))
            .IsRequired()
            .HasMaxLength(200);
            builder.Property(x => x.FullName)
                .HasConversion(x => x.Value, x => new FullName(x));
            builder.HasOne(x => x.Role)
                .WithOne(x => x.User)
                .HasForeignKey<AppUserRole>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);


            builder.Property(x => x.CreatedAt)
                .HasConversion(x => x.Value, x => new CustomDateTime(x));
            builder.Property(x => x.IsLocked)
                .IsRequired();
            builder.Property(x => x.Description)
                .HasConversion(x => x.Value, x => new Description(x))
                .HasMaxLength(1000);
            builder.HasOne(x => x.ContactData)
                .WithOne(x => x.User)
                .HasForeignKey<ContactData>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(x => x.Announcements)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.LikedAnnouncements)
                .WithOne(x => x.UserLiker)
                .HasForeignKey(x => x.UserLikerId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(x => x.LikedUsers)
                .WithOne(x => x.UserLiker)
                .HasForeignKey(x => x.UserLikedId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.ClientRatings)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(x => x.HelperRatings)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.ClientOffers)
                .WithOne(x => x.Client)
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
