using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Entities.Users;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Infrastructure.DAL
{
    public sealed class ICanHelpDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<ClientOffer> ClientOffers { get; set; }
        public DbSet<HelperOffer> HelperOffers { get; set; }
        public DbSet<ContactData> ContactData { get; set; }
        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<AppUserRole> AppUserRoles { get; set; }
        public DbSet<LikedUser> LikedUsers { get; set; }
        public DbSet<LikedAnnouncement> LikedAnnouncements { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Subcategory> Subcategories { get; set; }
        public DbSet<DifficultLevel> DifficultLevels { get; set; }
        public DbSet<Image> Images { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Rating> Ratings { get; set; }

        public ICanHelpDbContext(DbContextOptions dbContextOptions) : base (dbContextOptions)
        {
            
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ICanHelpDbContext).Assembly);
        }

    }
}
