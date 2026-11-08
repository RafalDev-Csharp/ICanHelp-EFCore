using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Events;
using ICanHelp.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace ICanHelp.Application;

public static class Extensions
{
    /// <summary>
    /// Registers all command handlers and domain event handlers (Krok 13) from this project.
    /// The host must also register an <see cref="INotificationSender"/> (Api: SignalR).
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.Scan(s => s.FromAssemblies(typeof(Extensions).Assembly)
            .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(s => s.FromAssemblies(typeof(Extensions).Assembly)
            .AddClasses(c => c.AssignableTo(typeof(IDomainEventHandler<>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.AddScoped<NotificationPublisher>();

        return services;
    }
}
