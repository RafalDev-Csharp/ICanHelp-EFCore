using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ICanHelp.Infrastructure.DAL.Configurations;

internal sealed class JobExitConfiguration : IEntityTypeConfiguration<JobExit>
{
    public void Configure(EntityTypeBuilder<JobExit> builder)
    {
        // F11 E: numbered per announcement (like AnnouncementChange per revision) – the pair is the key.
        builder.HasKey(x => new { x.AnnouncementId, x.Number });
        builder.Property(x => x.Number).ValueGeneratedNever();

        builder.Property(x => x.Kind).IsRequired();
        builder.Property(x => x.OfferId).IsRequired();
        builder.Property(x => x.HelperId).IsRequired();
        builder.Property(x => x.ActorId).IsRequired();
        builder.Property(x => x.ActorParty).IsRequired();
        builder.Property(x => x.Reason).IsRequired();
        builder.Property(x => x.Note).IsRequired(false);
        builder.Property(x => x.OccurredAt).IsRequired();
        builder.Property(x => x.Response).IsRequired(false);
        builder.Property(x => x.RespondedAt).IsRequired(false);

        // Who left – a reference to another aggregate (FK only). The helper id is a copy from the offer (no FK needed).
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ActorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Public counter "N rezygnacji / przerwanych zleceń" of a user.
        builder.HasIndex(x => x.ActorId);

        // The relation to Announcement is configured in AnnouncementConfiguration (the aggregate root).
    }
}
