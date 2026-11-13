using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Services;

/// <summary>A completed job of the profile owner: with whom and when it was accepted / confirmed.</summary>
public sealed record FinishedJob(Id AnnouncementId, UserId PartnerId, DateTime? AcceptedAt, DateTime? CompletedAt);

/// <summary>A rating the profile owner received for a job.</summary>
public sealed record ReceivedRating(Id AnnouncementId, UserId RaterId, int Rate, DateTime RatedAt);

/// <summary>
/// The reputation of a user in one role.
/// <see cref="RatingCount"/> – all received ratings, <see cref="RaterCount"/> – people whose rating is in the average
/// (one per person), <see cref="PartnerCount"/> – different partners of the completed jobs,
/// <see cref="MostJobsWithOnePartner"/> – the most completed jobs with the same person.
/// </summary>
public sealed record RoleReputation(
    double? AverageRating,
    int RatingCount,
    int RaterCount,
    int CompletedCount,
    int PartnerCount,
    int MostJobsWithOnePartner);

/// <summary>
/// F11 H: ratings resistant to collusion. Two accounts repeating "announcement → offer → accept → confirm → 5 stars"
/// should not build a reputation:
/// <list type="bullet">
///   <item>one person = one vote – only the newest counted rating from a given person enters the average;</item>
///   <item>a job confirmed very quickly after the acceptance (&lt; <see cref="MinJobDuration"/>) or a next job with
///         the same partner within <see cref="RepeatWindow"/> is suspicious – its rating is kept but not counted.</item>
/// </list>
/// Nobody is told "you cheat" – such a rating simply does not count. Jobs without times (before F11 H) are not suspicious.
/// </summary>
public static class ReputationPolicy
{
    public static readonly TimeSpan MinJobDuration = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan RepeatWindow = TimeSpan.FromDays(7);

    /// <summary>
    /// Suspicious jobs among all completed jobs of the user (both roles – partners could swap roles).
    /// </summary>
    public static IReadOnlySet<Id> FindSuspiciousJobs(IEnumerable<FinishedJob> jobs)
    {
        var suspicious = new HashSet<Id>();
        var completed = jobs.Where(j => j.CompletedAt is not null).ToList();

        foreach (var job in completed)
        {
            if (job.AcceptedAt is { } accepted && job.CompletedAt!.Value - accepted < MinJobDuration)
            {
                suspicious.Add(job.AnnouncementId);
                continue;
            }

            var repeated = completed.Any(other =>
                other.AnnouncementId != job.AnnouncementId
                && other.PartnerId == job.PartnerId
                && other.CompletedAt!.Value <= job.CompletedAt!.Value
                && job.CompletedAt.Value - other.CompletedAt.Value < RepeatWindow
                // The same moment: only the later announcement in a stable order is the repeat.
                && (other.CompletedAt.Value < job.CompletedAt.Value
                    || other.AnnouncementId.Value.CompareTo(job.AnnouncementId.Value) < 0));
            if (repeated)
            {
                suspicious.Add(job.AnnouncementId);
            }
        }

        return suspicious;
    }

    /// <summary>The reputation in one role: jobs and ratings of that role, suspicious jobs from all roles.</summary>
    public static RoleReputation Summarize(
        IReadOnlyCollection<FinishedJob> roleJobs,
        IReadOnlyCollection<ReceivedRating> roleRatings,
        IReadOnlySet<Id> suspiciousJobs)
    {
        var counted = roleRatings
            .Where(r => !suspiciousJobs.Contains(r.AnnouncementId))
            .GroupBy(r => r.RaterId)
            .Select(g => g.MaxBy(r => r.RatedAt)!.Rate)
            .ToList();

        var perPartner = roleJobs.GroupBy(j => j.PartnerId).Select(g => g.Count()).ToList();

        return new RoleReputation(
            AverageRating: counted.Count == 0 ? null : Math.Round(counted.Average(), 2),
            RatingCount: roleRatings.Count,
            RaterCount: counted.Count,
            CompletedCount: roleJobs.Count,
            PartnerCount: perPartner.Count,
            MostJobsWithOnePartner: perPartner.Count == 0 ? 0 : perPartner.Max());
    }
}
