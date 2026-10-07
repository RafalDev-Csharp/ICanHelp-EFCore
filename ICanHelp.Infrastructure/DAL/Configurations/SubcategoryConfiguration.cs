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
    internal class SubcategoryConfiguration : IEntityTypeConfiguration<Subcategory>
    {
        public void Configure(EntityTypeBuilder<Subcategory> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.SubcategoryName)
                .HasConversion(x => x.Value, x => new StringObject(x))
                .IsRequired()
                .HasMaxLength(100);

            builder.HasOne(x => x.Category)
                .WithMany(x => x.Subcategories)
                .HasForeignKey(x => x.CategoryId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
