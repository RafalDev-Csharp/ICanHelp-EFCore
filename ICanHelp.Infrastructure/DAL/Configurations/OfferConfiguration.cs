using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ICanHelp.Infrastructure.DAL.Configurations;

internal sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.ProposedBy).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.ProposedStart).IsRequired();
        builder.Property(x => x.ProposedEnd).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.Message).IsRequired(false);

        builder.ComplexProperty(x => x.Price, price =>
        {
            price.Property(p => p.Amount).HasColumnName("PriceAmount").HasPrecision(18, 2);
            price.Property(p => p.Currency).HasColumnName("PriceCurrency").HasMaxLength(3);
        });

        // The relation to Announcement is configured in AnnouncementConfiguration (the aggregate root).

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.HelperId)
            .OnDelete(DeleteBehavior.Restrict);

        // Counter-offer chain: an offer can point to the offer it answers.
        builder.HasOne<Offer>()
            .WithMany()
            .HasForeignKey(x => x.ParentOfferId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => new { x.AnnouncementId, x.HelperId, x.Status });
    }
}
