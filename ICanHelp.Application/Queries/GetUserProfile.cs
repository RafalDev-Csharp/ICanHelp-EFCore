using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Queries;

/// <summary>
/// F11 G: publiczny profil użytkownika – dane publiczne, statystyki osobno „Pomagam” / „Zlecam” (oceny tylko
/// z zakończonych zleceń, zakończone, przerwane i rezygnacje – przez niego / przez drugą stronę) i lista przerwanych
/// zleceń z powodem i stanowiskiem drugiej strony. Nieznany użytkownik → null → 404.
/// </summary>
public sealed class GetUserProfile : IQuery<UserProfileDto?>
{
    public Guid UserId { get; init; }
}
