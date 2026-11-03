using ICanHelp.Application.Abstractions;
using ICanHelp.Infrastructure.DAL;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ICanHelp.Infrastructure;

public static class Extensions
{
    /// <summary>Registers the database, repositories and query handlers. Called from the Api project.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSql(configuration);

        // Query handlers live here because they read straight from the DbContext.
        services.Scan(s => s.FromAssemblies(typeof(Extensions).Assembly)
            .AddClasses(c => c.AssignableTo(typeof(IQueryHandler<,>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.AddHostedService<DbInitializer>();

        return services;
    }
}
