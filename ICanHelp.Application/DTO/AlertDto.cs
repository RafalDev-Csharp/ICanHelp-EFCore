using ICanHelp.Core.Enums;

namespace ICanHelp.Application.DTO;

/// <summary>One alert of the reader (Krok 14). Area → <see cref="Center"/>; Route → <see cref="RoutePoints"/>.</summary>
public sealed record AlertDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public AlertKind Kind { get; init; }
    public Guid CategoryId { get; init; }

    /// <summary>Empty = the whole category.</summary>
    public IReadOnlyList<Guid> SubcategoryIds { get; init; } = [];

    public LocationDto? Center { get; init; }
    public IReadOnlyList<LocationDto> RoutePoints { get; init; } = [];
    public double RadiusKm { get; init; }
    public AlertScheduleDto Schedule { get; init; } = new();
    public bool IsEnabled { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// When the alert works (time zone of the app, <c>Alerts:TimeZone</c>). <see cref="Days"/> e.g. "Workdays" or
/// "Monday, Friday"; <see cref="From"/>/<see cref="To"/> e.g. "15:00:00"–"18:00:00" (both or none; none = all day).
/// </summary>
public sealed record AlertScheduleDto
{
    public WeekDays Days { get; init; } = WeekDays.All;
    public TimeOnly? From { get; init; }
    public TimeOnly? To { get; init; }
}
