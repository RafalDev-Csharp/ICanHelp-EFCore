using ICanHelp.Core.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ICanHelp.Infrastructure.DAL.Configurations;

internal sealed class UserDeviceConfiguration : IEntityTypeConfiguration<UserDevice>
{
    public void Configure(EntityTypeBuilder<UserDevice> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Token).IsRequired().HasMaxLength(UserDevice.MaxTokenLength);
        builder.Property(x => x.Platform).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.LastSeenAt).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // One FCM token = one device (registered again → the same row).
        builder.HasIndex(x => x.Token).IsUnique();

        // All devices of the recipient (push) and the limit of devices.
        builder.HasIndex(x => x.UserId);
    }
}
