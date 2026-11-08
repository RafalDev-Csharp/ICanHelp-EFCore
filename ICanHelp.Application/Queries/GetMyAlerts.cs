using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Queries;

/// <summary>Alerty zalogowanego użytkownika, od najnowszych.</summary>
public sealed record GetMyAlerts : IQuery<IReadOnlyList<AlertDto>>
{
    public Guid UserId { get; init; }
}

/// <summary>Jeden alert; cudzy albo nieistniejący → null (404).</summary>
public sealed record GetMyAlert : IQuery<AlertDto?>
{
    public Guid AlertId { get; init; }
    public Guid UserId { get; init; }
}

/// <summary>
/// Podgląd: otwarte ogłoszenia, które pasują do alertu teraz (strefa, kategoria, podkategorie; bez własnych),
/// od najnowszych, z odległością od kształtu alertu (punktu albo trasy). Cudzy / nieistniejący alert → null (404).
/// Harmonogram (dni/godziny) i to, czy alert jest włączony, nie mają tu znaczenia.
/// </summary>
public sealed record GetAlertPreview : IQuery<IReadOnlyList<AnnouncementDto>?>
{
    public Guid AlertId { get; init; }
    public Guid UserId { get; init; }
    public int Limit { get; init; } = 20;
}
