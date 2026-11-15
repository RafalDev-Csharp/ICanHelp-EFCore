using ICanHelp.Application.DTO;
using ICanHelp.Application.Time;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

/// <summary>
/// Dane z komend (DTO, zwykły tekst) → obiekty domeny. Jedno miejsce zamiast tego samego <c>new Price(...)</c>
/// w każdym handlerze; walidację robią konstruktory value objectów (błędne dane → wyjątek → 400 / 409).
/// </summary>
internal static class CommandMapping
{
    public static Price ToPrice(this PriceDto price) => new(price.Amount, price.Currency);

    public static Location ToLocation(this LocationDto location) => new(location.Latitude, location.Longitude);

    /// <summary>
    /// Tekst opcjonalny (wiadomość do oferty, komentarz do oceny, opis profilu): pusty / same spacje = brak,
    /// inaczej <see cref="Description"/> (2–500 znaków).
    /// </summary>
    public static Description? ToOptionalDescription(this string? text)
        => string.IsNullOrWhiteSpace(text) ? null : new Description(text);

    /// <summary>Warunki oferty z komendy; daty z JSON-a traktujemy jako UTC.</summary>
    public static OfferTerms ToTerms(this OfferTermsCommand command)
        => new(command.Price.ToPrice(), command.ProposedStart.AsUtc(), command.ProposedEnd.AsUtc(),
            command.Message.ToOptionalDescription());
}
