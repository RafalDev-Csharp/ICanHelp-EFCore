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
    internal class ContactDataConfiguration : IEntityTypeConfiguration<ContactData>
    {
        public void Configure(EntityTypeBuilder<ContactData> builder)
        {
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.Email).IsUnique();
            builder.Property(x => x.Email)
            .HasConversion(x => x.Value, x => new(x))
            .IsRequired()
            .HasMaxLength(100);
            builder.Property(x => x.PhoneNumber)
                .HasConversion(x => x.Value, x => new PhoneNumber(x))
                .IsRequired()
                .HasMaxLength(15);
            builder.HasOne(x => x.User)
                .WithOne(x => x.ContactData)
                .HasForeignKey<ContactData>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Property(x => x.Country)
                .HasConversion(x => x.Value, x => new StringObject(x))
                .HasMaxLength(50);
            builder.Property(x => x.City)
                .HasConversion(x => x.Value, x => new StringObject(x))
                .HasMaxLength(50);
            builder.Property(x => x.PostalCode)
                .HasConversion(x => x.Value, x => new StringObject(x))
                .HasMaxLength(20);
        }
    }
}
