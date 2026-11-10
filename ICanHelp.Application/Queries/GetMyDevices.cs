using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Queries;

/// <summary>Urządzenia zalogowanego użytkownika, od ostatnio widzianych.</summary>
public sealed record GetMyDevices : IQuery<IReadOnlyList<DeviceDto>>
{
    public Guid UserId { get; init; }
}

/// <summary>Moje urządzenie o danym tokenie (odpowiedź na rejestrację); brak / cudze → null.</summary>
public sealed record GetMyDeviceByToken : IQuery<DeviceDto?>
{
    public Guid UserId { get; init; }
    public string Token { get; init; } = string.Empty;
}
