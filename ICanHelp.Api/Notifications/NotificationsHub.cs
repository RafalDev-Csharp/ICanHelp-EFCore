using ICanHelp.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ICanHelp.Api.Notifications;

/// <summary>
/// Powiadomienia na żywo (Krok 13). Klient łączy się z <c>/hubs/notifications?access_token=JWT</c> i nasłuchuje
/// metody <c>notification</c> (ciało = <c>NotificationDto</c>). Każde połączenie trafia do grupy swojego użytkownika
/// (id z tokenu), więc wiadomość dostają wszystkie jego otwarte karty / urządzenia. Klient niczego nie wysyła.
/// Lista i licznik i tak są w <c>GET api/me/notifications</c> – hub tylko przyspiesza, nie zastępuje.
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    public const string Path = "/hubs/notifications";
    public const string NotificationMethod = "notification";

    public static string GroupOf(Guid userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        if (Context.User is { } user && user.TryGetUserId(out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupOf(userId));
        }

        await base.OnConnectedAsync();
    }
}
