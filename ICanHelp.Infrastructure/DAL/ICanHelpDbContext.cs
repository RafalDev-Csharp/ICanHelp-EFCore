using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Enums;
using ICanHelp.Core.ValueObjects;
using ICanHelp.Infrastructure.DAL.Converters;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL;

public sealed class ICanHelpDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<Image> Images => Set<Image>();
    public DbSet<AnnouncementChange> AnnouncementChanges => Set<AnnouncementChange>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Subcategory> Subcategories => Set<Subcategory>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<LikedAnnouncement> LikedAnnouncements => Set<LikedAnnouncement>();
    public DbSet<LikedUser> LikedUsers => Set<LikedUser>();

    public ICanHelpDbContext(DbContextOptions<ICanHelpDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ICanHelpDbContext).Assembly);
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
        // [Flags] enum as text, e.g. "Description, Budget" (Enum.Parse reads it back).
        configurationBuilder.Properties<AnnouncementFields>().HaveConversion<string>().HaveMaxLength(200);
    }
}
