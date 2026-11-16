using ICanHelp.Core.Enums;

namespace ICanHelp.Application.DTO;

/// <summary>Urządzenie z push (Krok 15). Tokenu nie zwracamy – zna go tylko samo urządzenie.</summary>
public sealed record DeviceDto
{
    public Guid Id { get; init; }
    public DevicePlatform Platform { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastSeenAt { get; init; }
}
