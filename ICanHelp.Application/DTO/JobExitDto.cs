namespace ICanHelp.Application.DTO;

/// <summary>
/// F11 E: one side left an accepted job – <c>Abandoned</c> before the start (the announcement went back to Open)
/// or <c>Interrupted</c> during the work (final status).
/// </summary>
public sealed record JobExitDto
{
    /// <summary>1, 2... in the order they happened.</summary>
    public int Number { get; init; }

    /// <summary>Abandoned or Interrupted.</summary>
    public string Kind { get; init; } = string.Empty;

    public Guid OfferId { get; init; }
    public Guid HelperId { get; init; }
    public Guid ActorId { get; init; }

    /// <summary>Who left: Client (owner) or Helper.</summary>
    public string ActorRole { get; init; } = string.Empty;

    /// <summary>CannotMakeIt, TermsDiffer, NoContact, Disagreement or Other.</summary>
    public string Reason { get; init; } = string.Empty;

    public string? Note { get; init; }
    public DateTime OccurredAt { get; init; }
}

/// <summary>
/// F11 E: public counters of a user – how many jobs he abandoned (before the start) and interrupted (during the work)
/// himself. Being left by the other side does not count.
/// </summary>
public sealed record UserReliabilityDto
{
    public Guid UserId { get; init; }
    public int AbandonedCount { get; init; }
    public int InterruptedCount { get; init; }
}
