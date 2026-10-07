using ICanHelp.Core.Repositories;
using ICanHelp.Infrastructure.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Infrastructure.DAL
{
    internal static class Extensions
    {
    private const string _sectionSql = "sqlSection";

        public static IServiceCollection AddSql(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ICanHelpDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString(_sectionSql)));

            services.AddDbContext<ICanHelpDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IAppUserRoleRepository, AppUserRoleRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IContactData, ContactData>();
            services.AddScoped<IDifficultLevelRepository, DifficultLevelRepository>();
            services.AddScoped<IImagesRepository, ImagesRepository>();
            services.AddScoped<ILikedAnnouncementRepository, LikedAnnouncementRepository>();
            services.AddScoped<ILikedUserRepository, LikedUserRepository>();
            services.AddScoped<ILocationRepository, LocationRepository>();
            services.AddScoped<IRatingRepository, RatingRepository>();
            services.AddScoped<ISubcategoryRepository, SubcategoryRepository>();
            services.AddScoped<IUserRepository, UserRepository>();

            services.AddScoped<IUnitOfWork, SqlUnitOfWork>();

            return services;
        }
    }
}
