using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Alerts;
using ICanHelp.Application.Notifications;
using ICanHelp.Application.Storage;
using ICanHelp.Application.Time;
using ICanHelp.Infrastructure.DAL;
using ICanHelp.Infrastructure.Notifications;
using ICanHelp.Infrastructure.Outbox;
using ICanHelp.Infrastructure.Security;
using ICanHelp.Infrastructure.Storage;
using ICanHelp.Infrastructure.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ICanHelp.Infrastructure;

public static class Extensions
{
    /// <summary>
    /// Registers the database, repositories, query handlers, clock, file storage, security (JWT) and the outbox.
    /// Called from the Api project.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSql(configuration);
        services.AddSecurity(configuration);
        services.AddSingleton<IClock, Clock>();

        // Uploaded images (Krok 10): folder from "Storage:ImagesPath".
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        // Query handlers live here because they read straight from the DbContext.
        services.Scan(s => s.FromAssemblies(typeof(Extensions).Assembly)
            .AddClasses(c => c.AssignableTo(typeof(IQueryHandler<,>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.AddHostedService<DbInitializer>();

        // Krok 13: outbox (events saved with the change → handled in the background → notifications).
        // Registered after DbInitializer: hosted services start in order, so migrations run first.
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddSingleton<IOutboxProcessor, OutboxProcessor>();
        services.AddHostedService<OutboxBackgroundService>();
        services.AddSingleton<IEmailSender, LoggingEmailSender>();

        // Krok 14: alerts about new announcements – daily limit, quiet hours, time zone (section "Alerts").
        services.AddSingleton(configuration.GetSection(AlertOptions.SectionName).Get<AlertOptions>() ?? new AlertOptions());

        return services;
    }
}
