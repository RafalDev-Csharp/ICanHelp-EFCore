using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Queries;

/// <summary>
/// Oceny, które otrzymał <see cref="UserId"/>, od najnowszych – z tytułem ogłoszenia i rolą, w jakiej go oceniono
/// (ekran „Oceny” w Koncie: segment „jako pomocnik” / „jako zleceniodawca”).
/// </summary>
public sealed record GetMyRatings : IQuery<IReadOnlyList<MyRatingDto>>
{
    /// <summary>Kto czyta (z tokenu).</summary>
    public Guid UserId { get; init; }
}

/// <summary>
/// „Do wystawienia”: zakończone ogłoszenia, w których <see cref="UserId"/> był stroną (właściciel albo przyjęty
/// pomocnik), a nie ocenił jeszcze drugiej strony.
/// </summary>
public sealed record GetMyPendingRatings : IQuery<IReadOnlyList<PendingRatingDto>>
{
    /// <summary>Kto czyta (z tokenu).</summary>
    public Guid UserId { get; init; }
}
