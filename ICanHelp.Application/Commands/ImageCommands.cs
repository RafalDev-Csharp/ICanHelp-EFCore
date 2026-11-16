using ICanHelp.Application.Abstractions;

namespace ICanHelp.Application.Commands;

/// <summary>
/// Nowe zdjęcie ogłoszenia (tylko właściciel, status Open, max 5). Plik: JPEG/PNG/WebP, max 5 MB – format rozpoznawany
/// po zawartości. Dodanie zdjęcia to zmiana treści: nowa rewizja, oczekujące oferty stają się nieaktualne.
/// Wszystkie pola ustawia kontroler (z trasy, tokenu i pliku z formularza).
/// </summary>
public sealed record AddAnnouncementImage : ICommand
{
    public Guid AnnouncementId { get; init; }
    public Guid ImageId { get; init; }
    public Guid ActorId { get; init; }

    /// <summary>Oryginalna nazwa pliku – tylko do wyświetlania.</summary>
    public string? FileName { get; init; }

    /// <summary>Zawartość pliku. Handler jej nie zamyka – robi to ten, kto ją otworzył.</summary>
    public Stream Content { get; init; } = Stream.Null;
}

/// <summary>Usunięcie zdjęcia (tylko właściciel, status Open). Też tworzy nową rewizję. Plik jest kasowany po zapisie.</summary>
public sealed record RemoveAnnouncementImage(Guid AnnouncementId, Guid ImageId, Guid ActorId) : ICommand;
