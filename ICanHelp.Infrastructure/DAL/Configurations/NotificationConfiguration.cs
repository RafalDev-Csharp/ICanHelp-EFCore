using ICanHelp.Core.Entities.Notifications;
using ICanHelp.Core.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ICanHelp.Infrastructure.DAL.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Type).IsRequired();
        builder.Property(x => x.Payload).IsRequired().HasMaxLength(Notification.MaxPayloadLength);
        builder.Property(x => x.AnnouncementId).IsRequired(false);
        builder.Property(x => x.ConversationId).IsRequired(false);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ReadAt).IsRequired(false);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // AnnouncementId / ConversationId without FK: the notification is history – it may outlive what it points to,
        // and one more cascade path to Users through Announcements is not allowed by SQL Server anyway.

        // Idempotency: one notification per recipient and event.
        builder.HasIndex(x => new { x.UserId, x.SourceEventId }).IsUnique();

        // The list (newest first), "unread only" and the counter.
        builder.HasIndex(x => new { x.UserId, x.ReadAt, x.CreatedAt });
    }
}
