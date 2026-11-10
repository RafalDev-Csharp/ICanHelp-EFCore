using System.Text.Json.Serialization;
using ICanHelp.Application.Abstractions;
using ICanHelp.Core.Enums;

namespace ICanHelp.Application.Commands;

/// <summary>
/// Krok 15: „to urządzenie ma dostawać push” – token FCM z aplikacji. Idempotentne: ten sam token jeszcze raz odświeża
/// <c>LastSeenAt</c> (a gdy zalogował się na nim ktoś inny – przechodzi na niego). Max 10 urządzeń – najdawniej
/// widziane wypada. <see cref="DeviceId"/> = id nowego urządzenia (ustawia kontroler), istniejące zachowuje swoje.
/// </summary>
public sealed record RegisterDevice : ICommand
{
    [JsonIgnore] public Guid DeviceId { get; init; }
    [JsonIgnore] public Guid UserId { get; init; }

    public string Token { get; init; } = string.Empty;
    public DevicePlatform Platform { get; init; } = DevicePlatform.Web;
}

/// <summary>Wylogowanie / wyłączenie powiadomień na urządzeniu. Cudze albo nieistniejące → 404.</summary>
public sealed record RemoveDevice(Guid DeviceId, Guid UserId) : ICommand;
