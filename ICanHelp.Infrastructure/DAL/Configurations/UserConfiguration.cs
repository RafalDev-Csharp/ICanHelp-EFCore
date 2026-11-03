using ICanHelp.Core.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ICanHelp.Infrastructure.DAL.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.UserName).IsRequired();
        builder.HasIndex(x => x.UserName).IsUnique();

        builder.Property(x => x.FullName).IsRequired();
        builder.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Role).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.IsLocked).IsRequired();
        builder.Property(x => x.Description).IsRequired(false);

        // ContactData has no identity: it is stored in the Users table.
        // OwnsOne (not ComplexProperty) because we need a unique index on Email.
        builder.OwnsOne(x => x.ContactData, contact =>
        {
            contact.Property(c => c.Email).HasColumnName("Email").IsRequired();
            contact.Property(c => c.PhoneNumber).HasColumnName("PhoneNumber").IsRequired();
            contact.Property(c => c.Country).HasColumnName("Country").HasMaxLength(50).IsRequired();
            contact.Property(c => c.City).HasColumnName("City").HasMaxLength(50).IsRequired();
            contact.Property(c => c.PostalCode).HasColumnName("PostalCode").HasMaxLength(20).IsRequired();

            contact.HasIndex(c => c.Email).IsUnique();
        });
        builder.Navigation(x => x.ContactData).IsRequired();
    }
}
