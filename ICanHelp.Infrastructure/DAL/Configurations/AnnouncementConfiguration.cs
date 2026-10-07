using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//Gotchas to check
//•	Does Category actually expose a ICollection<Announcement> Announcements property? If not, WithMany(x => x.Announcements) will not compile — use WithMany() (no lambda) or add the collection navigation to Category.
//•	CategoryId is a value object type (Id wrapping a Guid). EF Core needs either a value converter or a global mapping for that type. If you don't have a converter for Id, EF will not know how to persist it. Ensure you have configured a ValueConverter<Id, Guid> somewhere (or configure CategoryId explicitly).
//•	If you want Category deletions to be prevented when announcements exist, set .OnDelete(DeleteBehavior.Restrict).
//•	If you want the relationship to be optional, make CategoryId nullable (e.g., Id?) or call .IsRequired(false).

namespace ICanHelp.Infrastructure.DAL.Configurations
{
    internal class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
    {
        public void Configure(EntityTypeBuilder<Announcement> builder)
        {
            builder.HasKey(x => x.Id);

            builder.HasOne(x => x.Category)
                .WithMany(x => x.Announcements)
                .HasForeignKey(x => x.CategoryId);
            builder.HasOne(x => x.Subcategory)
                .WithMany()
                .HasForeignKey(x => x.SubcategoryId);
            builder.HasOne(x => x.DifficultLevel)
                .WithMany(x => x.Announcements)
                .HasForeignKey(x => x.DifficultLevelId);
            builder.HasOne(x => x.User)
                .WithMany(x => x.Announcements)
                .HasForeignKey(x => x.OwnerId);
            builder.Property(x => x.Title)
                .HasConversion(x => x.Value, x => new StringObject(x))
                .IsRequired()
                .HasMaxLength(100);
            builder.Property(x => x.CreatedAt)
                .IsRequired();
            builder.HasOne(x => x.Location)
                .WithOne(x => x.Announcement)
                .HasForeignKey<Location>(x => x.Id)
                .IsRequired();
            builder.Property(x => x.Description)
                .HasConversion(x => x.Value, x => new Description(x))
                .IsRequired()
                .HasMaxLength(1000);

            builder.Property(x => x.Price)
                .IsRequired();

            builder.Property(x => x.IsAvailable)
                .IsRequired();
            builder.Property(x => x.IsFinished)
                .IsRequired();
            builder.HasOne(x => x.ClientOffer)
                .WithOne(x => x.Announcement)
                .HasForeignKey<ClientOffer>(x => x.AnnouncementId);
            builder.HasMany(x => x.HelperOffers)
                .WithOne(x => x.Announcement)
                .HasForeignKey(x => x.AnnouncementId);
            builder.HasMany(x => x.Images)
                .WithOne(x => x.Announcement)
                .HasForeignKey(x => x.AnnouncementId);


        }
    }
}
