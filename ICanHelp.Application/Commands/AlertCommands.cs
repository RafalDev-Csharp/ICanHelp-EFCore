using System.Text.Json.Serialization;
using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Core.Enums;

namespace ICanHelp.Application.Commands;

/// <summary>
/// Nowy alert o ogłoszeniach (Krok 14). <c>Area</c> – podaj <see cref="Center"/>; <c>Route</c> – podaj
/// <see cref="RoutePoints"/> (polilinia z serwisu routingu, min. 2 punkty). Puste <see cref="SubcategoryIds"/> = cała kategoria.
/// </summary>
public sealed record CreateAlert : ICommand
{
    [JsonIgnore] public Guid AlertId { get; init; }
    [JsonIgnore] public Guid UserId { get; init; }

    public string Name { get; init; } = string.Empty;
    public AlertKind Kind { get; init; } = AlertKind.Area;
    public Guid CategoryId { get; init; }
    public IReadOnlyList<Guid>? SubcategoryIds { get; init; }
    public LocationDto? Center { get; init; }
    public IReadOnlyList<LocationDto>? RoutePoints { get; init; }
    public double RadiusKm { get; init; } = 5;

    /// <summary>Brak = codziennie, całą dobę.</summary>
    public AlertScheduleDto? Schedule { get; init; }
}

/// <summary>
/// Częściowa zmiana alertu (PATCH): <c>null</c> = bez zmian. Rodzaju (Area/Route) zmienić się nie da.
/// Zmiana kategorii bez <see cref="SubcategoryIds"/> czyści podkategorie (należały do starej kategorii).
/// <see cref="Schedule"/> podany = zastępuje cały harmonogram. Zmiana punktów/promienia przelicza strefę w bazie.
/// </summary>
public sealed record UpdateAlert : ICommand
{
    [JsonIgnore] public Guid AlertId { get; init; }
    [JsonIgnore] public Guid UserId { get; init; }

    public string? Name { get; init; }
    public Guid? CategoryId { get; init; }
    public IReadOnlyList<Guid>? SubcategoryIds { get; init; }
    public LocationDto? Center { get; init; }
    public IReadOnlyList<LocationDto>? RoutePoints { get; init; }
    public double? RadiusKm { get; init; }
    public AlertScheduleDto? Schedule { get; init; }
}

public sealed record DeleteAlert(Guid AlertId, Guid UserId) : ICommand;

/// <summary>Włączenie / wyłączenie alertu (wyłączony nie wysyła powiadomień, ale zostaje na liście).</summary>
public sealed record SetAlertEnabled(Guid AlertId, Guid UserId, bool IsEnabled) : ICommand;
