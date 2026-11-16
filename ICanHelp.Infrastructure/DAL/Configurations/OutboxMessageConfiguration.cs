using ICanHelp.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ICanHelp.Infrastructure.DAL.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Type).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Payload).IsRequired(); // nvarchar(max)
        builder.Property(x => x.OccurredAt).IsRequired();
        builder.Property(x => x.ProcessedAt).IsRequired(false);
        builder.Property(x => x.Error).IsRequired(false).HasMaxLength(OutboxMessage.MaxErrorLength);

        // "What is waiting, oldest first".
        builder.HasIndex(x => new { x.ProcessedAt, x.OccurredAt });
    }
}
