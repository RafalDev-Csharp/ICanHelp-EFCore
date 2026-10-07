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
    internal class DifficultLevelConfiguration : IEntityTypeConfiguration<DifficultLevel>
    {
        public void Configure(EntityTypeBuilder<DifficultLevel> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.DifficultLevelName)
                .HasConversion(x => x.Value, x => new StringObject(x))
                .IsRequired()
                .HasMaxLength(100);
            builder.HasMany(x => x.Announcements)
                .WithOne(x => x.DifficultLevel)
                .HasForeignKey(x => x.DifficultLevelId);
        }
    }
}
