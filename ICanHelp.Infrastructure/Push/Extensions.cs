using ICanHelp.Application.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ICanHelp.Infrastructure.Push;

internal static class Extensions
{
    /// <summary>Krok 15: FCM when <c>Push:Enabled</c> and a key are given, otherwise push only goes to the log.</summary>
    public static IServiceCollection AddPush(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(PushOptions.SectionName);
        services.Configure<PushOptions>(section);

        if (section.Get<PushOptions>() is { UsesFcm: true })
        {
            services.AddSingleton<IPushSender, FcmPushSender>();
        }
        else
        {
            services.AddSingleton<IPushSender, LoggingPushSender>();
        }

        return services;
    }
}
