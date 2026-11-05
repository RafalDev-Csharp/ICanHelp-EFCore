using System.Text.Json.Serialization;
using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Core.Enums;

namespace ICanHelp.Application.Commands;

// Pola z [JsonIgnore] ustawia kontroler: id nowych zasobów, id z trasy i id zalogowanego użytkownika (claim "sub").
// Klient nigdy nie podaje, w czyim imieniu działa.

/// <summary>Nowe ogłoszenie. Właścicielem jest zalogowany użytkownik.</summary>
public sealed record CreateAnnouncement : ICommand
{
    [JsonIgnore] public Guid AnnouncementId { get; init; }
    [JsonIgnore] public Guid OwnerId { get; init; }

    public Guid CategoryId { get; init; }
    public Guid SubcategoryId { get; init; }
    public DifficultyLevel Difficulty { get; init; } = DifficultyLevel.Easy;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public PriceDto Budget { get; init; } = new();
    public LocationDto Location { get; init; } = new();
}

/// <summary>Anulowanie ogłoszenia (tylko właściciel, status Open albo Accepted).</summary>
public sealed record CancelAnnouncement(Guid AnnouncementId, Guid ActorId) : ICommand;

/// <summary>Rozpoczęcie pracy (właściciel albo pomocnik z zaakceptowanej oferty).</summary>
public sealed record StartAnnouncement(Guid AnnouncementId, Guid ActorId) : ICommand;

/// <summary>Potwierdzenie wykonania (tylko właściciel). Potem obie strony mogą się ocenić.</summary>
public sealed record CompleteAnnouncement(Guid AnnouncementId, Guid ActorId) : ICommand;

/// <summary>Ocena drugiej strony po zakończeniu ogłoszenia (raz na ogłoszenie).</summary>
public sealed record RateAnnouncement : ICommand
{
    [JsonIgnore] public Guid AnnouncementId { get; init; }
    [JsonIgnore] public Guid RatingId { get; init; }
    [JsonIgnore] public Guid RaterId { get; init; }

    /// <summary>1–5.</summary>
    public int Rate { get; init; }
    public string? Comment { get; init; }
}
