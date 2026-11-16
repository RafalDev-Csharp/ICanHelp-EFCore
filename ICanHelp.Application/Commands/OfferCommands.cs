using System.Text.Json.Serialization;
using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Commands;

/// <summary>
/// Warunki oferty – te same pola w pierwszej ofercie (<see cref="MakeOffer"/>), kontrofercie (<see cref="CounterOffer"/>)
/// i zmienionej ofercie (<see cref="ReviseOffer"/>).
/// </summary>
public abstract record OfferTermsCommand : ICommand
{
    public PriceDto Price { get; init; } = new();

    /// <summary>UTC (np. "2026-11-20T16:00:00Z"); czas bez strefy traktujemy jako UTC.</summary>
    public DateTime ProposedStart { get; init; }
    public DateTime ProposedEnd { get; init; }
    public string? Message { get; init; }
}

/// <summary>Pierwsza oferta pomocnika (zalogowany użytkownik) do ogłoszenia.</summary>
public sealed record MakeOffer : OfferTermsCommand
{
    [JsonIgnore] public Guid AnnouncementId { get; init; }
    [JsonIgnore] public Guid OfferId { get; init; }
    [JsonIgnore] public Guid HelperId { get; init; }
}

/// <summary>Kontroferta drugiej strony do oczekującej oferty <see cref="OfferId"/>.</summary>
public sealed record CounterOffer : OfferTermsCommand
{
    [JsonIgnore] public Guid AnnouncementId { get; init; }
    [JsonIgnore] public Guid OfferId { get; init; }
    [JsonIgnore] public Guid NewOfferId { get; init; }
    [JsonIgnore] public Guid ActorId { get; init; }
}

/// <summary>
/// Autor aktualizuje własną nieaktualną ofertę <see cref="OfferId"/> (ogłoszenie zmieniło się po jej złożeniu) –
/// nowe warunki na bieżącej wersji ogłoszenia, stara oferta → <c>Revised</c>.
/// </summary>
public sealed record ReviseOffer : OfferTermsCommand
{
    [JsonIgnore] public Guid AnnouncementId { get; init; }
    [JsonIgnore] public Guid OfferId { get; init; }
    [JsonIgnore] public Guid NewOfferId { get; init; }
    [JsonIgnore] public Guid ActorId { get; init; }
}

/// <summary>Akceptacja oferty przez drugą stronę niż jej autor.</summary>
public sealed record AcceptOffer(Guid AnnouncementId, Guid OfferId, Guid ActorId) : ICommand;

/// <summary>Odrzucenie oferty przez drugą stronę niż jej autor.</summary>
public sealed record RejectOffer(Guid AnnouncementId, Guid OfferId, Guid ActorId) : ICommand;

/// <summary>Wycofanie oferty przez jej autora.</summary>
public sealed record WithdrawOffer(Guid AnnouncementId, Guid OfferId, Guid ActorId) : ICommand;
