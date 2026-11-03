using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ICanHelp.Infrastructure.DAL.Configurations;

internal sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Title).IsRequired();
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.Difficulty).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        // Value objects with more than one field are stored in the same table (columns BudgetAmount, BudgetCurrency...).
        builder.ComplexProperty(x => x.Budget, budget =>
        {
            budget.Property(p => p.Amount).HasColumnName("BudgetAmount").HasPrecision(18, 2);
            budget.Property(p => p.Currency).HasColumnName("BudgetCurrency").HasMaxLength(3);
        });

        builder.ComplexProperty(x => x.Location, location =>
        {
            location.Property(l => l.Latitude).HasColumnName("Latitude");
            location.Property(l => l.Longitude).HasColumnName("Longitude");
        });

        // References to other aggregates: only the foreign key, no navigation property.
        // Restrict = you cannot delete a user/category that still has announcements.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Subcategory>()
            .WithMany()
            .HasForeignKey(x => x.SubcategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Parts of the aggregate: deleted together with the announcement.
        builder.HasMany(x => x.Offers)
            .WithOne()
            .HasForeignKey(o => o.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Offers).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.Images)
            .WithOne()
            .HasForeignKey(i => i.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Images).UsePropertyAccessMode(PropertyAccessMode.Field);

        // The accepted offer. NoAction, because offers are already removed by the cascade above.
        builder.HasOne<Offer>()
            .WithMany()
            .HasForeignKey(x => x.AcceptedOfferId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        // Typical list query: open announcements, newest first.
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => x.OwnerId);
    }
}
