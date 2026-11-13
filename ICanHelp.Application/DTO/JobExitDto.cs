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

    /// <summary>F11 G: the statement of the other side (null until given – once).</summary>
    public string? Response { get; init; }
    public DateTime? RespondedAt { get; init; }
}

/// <summary>
/// F11 G: public profile – what everybody may see about a user: public data, statistics separately for both roles
/// and the list of interrupted jobs with both statements.
/// </summary>
public sealed record UserProfileDto
{
    public UserPublicDto User { get; init; } = new();

    /// <summary>When the user was the helper („Pomagam”).</summary>
    public RoleStatsDto AsHelper { get; init; } = new();

    /// <summary>When the user was the client („Zlecam”).</summary>
    public RoleStatsDto AsClient { get; init; } = new();

    /// <summary>Interrupted jobs (in either role), newest first.</summary>
    public IReadOnlyList<ProfileInterruptionDto> Interruptions { get; init; } = [];
}

/// <summary>F11 G: statistics of a user in one role. Ratings only from completed jobs.</summary>
public sealed record RoleStatsDto
{
    /// <summary>Average of received ratings (null without ratings).</summary>
    public double? AverageRating { get; init; }
    public int RatingCount { get; init; }
    public int CompletedCount { get; init; }

    /// <summary>Interrupted by this user himself / by the other side.</summary>
    public int InterruptedByUser { get; init; }
    public int InterruptedByOther { get; init; }

    /// <summary>Abandoned before the start by this user himself / by the other side.</summary>
    public int AbandonedByUser { get; init; }
    public int AbandonedByOther { get; init; }
}

/// <summary>F11 G: one interrupted job on a profile: the user's role, who interrupted, why, and the other statement.</summary>
public sealed record ProfileInterruptionDto
{
    public Guid AnnouncementId { get; init; }
    public string AnnouncementTitle { get; init; } = string.Empty;

    /// <summary>The profile owner's role in this job: Client or Helper.</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>True when the profile owner interrupted the job himself.</summary>
    public bool ByUser { get; init; }

    /// <summary>The whole entry (reason, note, response).</summary>
    public JobExitDto Exit { get; init; } = new();
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
