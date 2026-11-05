using ICanHelp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ICanHelp.Infrastructure.DAL.Configurations;

internal sealed class SubcategoryConfiguration : IEntityTypeConfiguration<Subcategory>
{
    public void Configure(EntityTypeBuilder<Subcategory> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.SubcategoryName).IsRequired();

        // The relation to Category is configured in CategoryConfiguration (the aggregate root).
        builder.HasIndex(x => new { x.CategoryId, x.SubcategoryName }).IsUnique();
    }
}
