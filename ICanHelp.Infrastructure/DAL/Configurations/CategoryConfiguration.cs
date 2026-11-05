using ICanHelp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ICanHelp.Infrastructure.DAL.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CategoryName).IsRequired();
        builder.HasIndex(x => x.CategoryName).IsUnique();

        builder.HasMany(x => x.Subcategories)
            .WithOne()
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Subcategories).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
