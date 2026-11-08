using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Alerts;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.ValueObjects;
using ICanHelp.Infrastructure.DAL.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
// NetTopologySuite.Geometries ma własny typ Location – tu chodzi o punkt z domeny.
using Location = ICanHelp.Core.ValueObjects.Location;

namespace ICanHelp.Infrastructure.DAL.Configurations;

/// <summary>
/// Krok 14. Geometry lives only in the database:
/// <list type="bullet">
/// <item><c>Shape</c> (geography: point or LINESTRING) – written by EF, computed from <c>Points</c> in
/// <see cref="ICanHelpDbContext"/> before saving (<see cref="Spatial.CreateShape"/>);</item>
/// <item><c>Zone</c> (geography: Shape buffered by RadiusKm in metres) – computed by SQL Server in the trigger
/// <see cref="Spatial.AlertZoneTrigger"/> (NTS would buffer in degrees; a computed column with a geography method cannot
/// be PERSISTED, so it could not have a spatial index). EF never writes it. Spatial index
/// <c>IX_AnnouncementAlerts_Zone</c> – in the migration (EF cannot model spatial indexes).</item>
/// </list>
/// </summary>
internal sealed class AnnouncementAlertConfiguration : IEntityTypeConfiguration<AnnouncementAlert>
{
    public void Configure(EntityTypeBuilder<AnnouncementAlert> builder)
    {
        // The trigger must be declared: EF then does not use OUTPUT in INSERT/UPDATE (not allowed with triggers).
        builder.ToTable("AnnouncementAlerts", t => t.HasTrigger(Spatial.AlertZoneTrigger));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.Kind).IsRequired();
        builder.Property(x => x.RadiusKm).IsRequired();
        builder.Property(x => x.ActiveDays).IsRequired();
        builder.Property(x => x.ActiveFrom).IsRequired(false);
        builder.Property(x => x.ActiveTo).IsRequired(false);
        builder.Property(x => x.IsEnabled).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired(false);

        builder.Property(x => x.Points)
            .HasConversion(new LocationListConverter(), new ListComparer<Location>())
            .IsRequired();

        builder.Property(x => x.SubcategoryIds)
            .HasConversion(new IdListConverter(), new ListComparer<Id>())
            .IsRequired();

        // Views of Points for the readers – not columns.
        builder.Ignore(x => x.Center);
        builder.Ignore(x => x.RoutePoints);

        builder.Property<Geometry>(Spatial.AlertShape)
            .HasColumnType("geography")
            .IsRequired();

        var zone = builder.Property<Geometry>(Spatial.AlertZone)
            .HasColumnType("geography")
            .IsRequired(false);
        zone.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        zone.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.UserId);
    }
}
