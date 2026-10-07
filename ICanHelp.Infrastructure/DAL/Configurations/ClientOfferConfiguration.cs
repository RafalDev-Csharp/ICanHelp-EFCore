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
    internal class ClientOfferConfiguration : IEntityTypeConfiguration<ClientOffer>
    {
        public void Configure(EntityTypeBuilder<ClientOffer> builder)
        {
            builder.HasKey(x => x.Id);

            builder.HasOne(x => x.Announcement)
                   .WithOne(a => a.ClientOffer)
                   .HasForeignKey<ClientOffer>(x => x.AnnouncementId)
                   .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.Client)
                   .WithMany(c => c.ClientOffers)
                   .HasForeignKey(x => x.ClientId)
                   .OnDelete(DeleteBehavior.Cascade);
            //builder.HasOne(x => x.Client)
            //       .WithMany(h => h.HelperOffers )
            //       .HasForeignKey(x => x.HelperId)
            //       .OnDelete(DeleteBehavior.Cascade);
            //builder.HasOne(x => x.Helper)
            //       .WithMany(h => h.HelperOffers)
            //       .HasForeignKey(x => x.HelperId)
            //       .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.Helper)
                   .WithMany(c => c.ClientOffers)
                   .HasForeignKey(x => x.ClientId)
                   .OnDelete(DeleteBehavior.Cascade);
            builder.Property(x => x.WantToStart)
                     .HasConversion(
                          v => v.Value,
                          v => new CustomDateTime(v))
                     .IsRequired();
            builder.Property(x => x.WantToFinish)
                        .HasConversion(
                            v => v.Value,
                            v => new CustomDateTime(v))
                     .IsRequired();
            builder.Property(x => x.Price)
                     .IsRequired();
            builder.Property(x => x.Description)
                        .IsRequired()
                        .HasMaxLength(1000);
            builder.HasOne(x => x.CreatedBy)
                   .WithMany(x => x.ClientOffers)
                   .HasForeignKey(x => x.CreatedById)
                   .OnDelete(DeleteBehavior.Restrict);
            //builder.HasOne(x => x.AcceptedBy)
            //       .WithMany(x => x.HelperOffers)
            //       .HasForeignKey(x => x.AcceptedById)
            //       .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.RatingHelper)
                   .WithOne(r => r.ClientOffer)
                   .HasForeignKey<ClientOffer>(x => x.RatingHelperId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.Property(x => x.IsAccepted)
            .IsRequired();



        }
    }
}
