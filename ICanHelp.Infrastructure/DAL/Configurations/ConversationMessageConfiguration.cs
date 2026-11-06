using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ICanHelp.Infrastructure.DAL.Configurations;

internal sealed class ConversationMessageConfiguration : IEntityTypeConfiguration<ConversationMessage>
{
    public void Configure(EntityTypeBuilder<ConversationMessage> builder)
    {
        // No DbSet for messages, so the table name is given explicitly.
        builder.ToTable("ConversationMessages");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Text).IsRequired();
        builder.Property(x => x.SentAt).IsRequired();
        builder.Property(x => x.AnnouncementRevision).IsRequired();

        // The relation to Conversation is configured in ConversationConfiguration (the aggregate root).

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Messages of a conversation in order; also the last message for "my conversations" (Step 12).
        builder.HasIndex(x => new { x.ConversationId, x.SentAt });
    }
}
