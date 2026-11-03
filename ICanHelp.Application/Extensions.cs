using ICanHelp.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ICanHelp.Application;

public static class Extensions
{
    /// <summary>Registers all command handlers from this project (added in the next steps).</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.Scan(s => s.FromAssemblies(typeof(Extensions).Assembly)
            .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        return services;
    }
}
