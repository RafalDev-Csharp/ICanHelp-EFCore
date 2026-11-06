using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ICanHelp.Infrastructure.DAL.Configurations;

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ClientLastReadAt).IsRequired(false);
        builder.Property(x => x.HelperLastReadAt).IsRequired(false);

        // The conversation lives as long as its announcement (like ratings).
        builder.HasOne<Announcement>()
            .WithMany()
            .HasForeignKey(x => x.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);

        // Two relations to Users: SQL Server does not allow two cascade paths, so both are Restrict.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.HelperId)
            .OnDelete(DeleteBehavior.Restrict);

        // Part of the aggregate: deleted together with the conversation.
        builder.HasMany(x => x.Messages)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Messages).UsePropertyAccessMode(PropertyAccessMode.Field);

        // One conversation per helper and announcement.
        builder.HasIndex(x => new { x.AnnouncementId, x.HelperId }).IsUnique();

        // "My conversations" (Step 12): as the client or as the helper.
        builder.HasIndex(x => x.ClientId);
        builder.HasIndex(x => x.HelperId);
    }
}
