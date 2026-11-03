using ICanHelp.Core.Repositories;
using ICanHelp.Infrastructure.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ICanHelp.Infrastructure.DAL;

internal static class Extensions
{
    private const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddSql(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is missing.");

        services.AddDbContext<ICanHelpDbContext>(options => options.UseSqlServer(connectionString));

        services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRatingRepository, RatingRepository>();
        services.AddScoped<ILikedAnnouncementRepository, LikedAnnouncementRepository>();
        services.AddScoped<ILikedUserRepository, LikedUserRepository>();

        services.AddScoped<IUnitOfWork, SqlUnitOfWork>();

        return services;
    }
}
