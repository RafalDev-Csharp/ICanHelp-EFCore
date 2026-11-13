using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Alerts;
using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Entities.Notifications;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Enums;
using ICanHelp.Core.ValueObjects;
using ICanHelp.Infrastructure.DAL.Converters;
using ICanHelp.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL;

public sealed class ICanHelpDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<Image> Images => Set<Image>();
    public DbSet<AnnouncementChange> AnnouncementChanges => Set<AnnouncementChange>();
    public DbSet<JobExit> JobExits => Set<JobExit>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Subcategory> Subcategories => Set<Subcategory>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<LikedAnnouncement> LikedAnnouncements => Set<LikedAnnouncement>();
    public DbSet<LikedUser> LikedUsers => Set<LikedUser>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<AnnouncementAlert> AnnouncementAlerts => Set<AnnouncementAlert>();

    public ICanHelpDbContext(DbContextOptions<ICanHelpDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ICanHelpDbContext).Assembly);

        // Domain events live only in memory until SqlUnitOfWork moves them to the outbox – not a column, not a relation.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(AggregateRoot).IsAssignableFrom(t.ClrType))
                     .ToList())
        {
            modelBuilder.Entity(entityType.ClrType).Ignore(nameof(AggregateRoot.DomainEvents));
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAlertShapes();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAlertShapes();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Krok 14: the geography shape of a new alert or of an alert with changed points (point or LINESTRING). The zone
    /// (shape + radius in metres) is computed afterwards by the trigger in SQL Server, in the same transaction.
    /// </summary>
    private void ApplyAlertShapes()
    {
        // Entries() runs DetectChanges, so IsModified is up to date.
        foreach (var entry in ChangeTracker.Entries<AnnouncementAlert>())
        {
            var pointsChanged = entry.State == EntityState.Added
                                || (entry.State == EntityState.Modified && entry.Property(a => a.Points).IsModified);

            if (pointsChanged)
            {
                entry.Property(Spatial.AlertShape).CurrentValue = Spatial.CreateShape(entry.Entity.Points);
            }
        }
    }

    // Conventions apply to every property of the given type in the whole model,
    // so the entity configurations do not have to repeat HasConversion for each value object.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Id>().HaveConversion<IdConverter>();
        configurationBuilder.Properties<UserId>().HaveConversion<UserIdConverter>();

        configurationBuilder.Properties<StringObject>().HaveConversion<StringObjectConverter>().HaveMaxLength(100);
        configurationBuilder.Properties<Description>().HaveConversion<DescriptionConverter>().HaveMaxLength(500);
        configurationBuilder.Properties<UserName>().HaveConversion<UserNameConverter>().HaveMaxLength(30);
        configurationBuilder.Properties<FullName>().HaveConversion<FullNameConverter>().HaveMaxLength(100);
        configurationBuilder.Properties<Email>().HaveConversion<EmailConverter>().HaveMaxLength(100);
        configurationBuilder.Properties<PhoneNumber>().HaveConversion<PhoneNumberConverter>().HaveMaxLength(16);
        configurationBuilder.Properties<Rate>().HaveConversion<RateConverter>();

        // Enums as text: the database stays readable ("Open" instead of 1).
        configurationBuilder.Properties<AnnouncementStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<OfferStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<OfferParty>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<DifficultyLevel>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<UserRole>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<NotificationType>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<AlertKind>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<DevicePlatform>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<JobExitKind>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<JobExitReason>().HaveConversion<string>().HaveMaxLength(20);
        // [Flags] as text, e.g. "Workdays" or "Monday, Saturday".
        configurationBuilder.Properties<WeekDays>().HaveConversion<string>().HaveMaxLength(100);
        // [Flags] enum as text, e.g. "Description, Budget" (Enum.Parse reads it back).
        configurationBuilder.Properties<AnnouncementFields>().HaveConversion<string>().HaveMaxLength(200);
    }
}
