using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Events;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.Services;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities;

// The job after an offer is accepted: start, report, confirmation, giving up (exits), cancelling and rating.
public partial class Announcement
{
    /// <summary>Work begins – only the helper of the accepted offer marks it (he is the one who comes and works).</summary>
    public void Start(UserId actorId, DateTime startedAtUtc)
    {
        EnsureStatus(AnnouncementStatus.Accepted, "be started");
        var helperId = EnsureAcceptedHelper(actorId, "start");

        Status = AnnouncementStatus.InProgress;
        AddDomainEvent(new JobStarted(Id, OwnerId, helperId, startedAtUtc));
    }

    /// <summary>
    /// The helper says the work is done and waits for the owner's confirmation (<see cref="Complete"/>).
    /// The status stays <see cref="AnnouncementStatus.InProgress"/>; only once.
    /// </summary>
    public void ReportDone(UserId actorId, DateTime reportedAtUtc)
    {
        EnsureStatus(AnnouncementStatus.InProgress, "be reported as done");
        var helperId = EnsureAcceptedHelper(actorId, "report");

        if (DoneReportedAt is not null)
        {
            throw new DomainRuleException("The work has already been reported as done.");
        }

        DoneReportedAt = reportedAtUtc;
        AddDomainEvent(new JobDoneReported(Id, OwnerId, helperId, reportedAtUtc));
    }

    /// <summary>
    /// Only the owner (the client) confirms that the work is done. Also from <see cref="AnnouncementStatus.Accepted"/>:
    /// the helper may forget to press "start" / "done" – the job cannot get stuck because of it.
    /// </summary>
    public void Complete(UserId actorId, DateTime completedAtUtc)
    {
        if (Status is not (AnnouncementStatus.Accepted or AnnouncementStatus.InProgress))
        {
            throw new DomainRuleException($"Announcement in status {Status} cannot be completed.");
        }

        EnsureOwner(actorId, "complete");

        Status = AnnouncementStatus.Completed;
        CompletedAt = completedAtUtc;
        AddDomainEvent(new JobCompleted(Id, OwnerId, GetAcceptedHelperId()!, completedAtUtc));
    }

    public void Cancel(UserId actorId)
    {
        if (Status is not (AnnouncementStatus.Open or AnnouncementStatus.Accepted))
        {
            throw new DomainRuleException($"Announcement in status {Status} cannot be cancelled.");
        }

        EnsureOwner(actorId, "cancel");

        Status = AnnouncementStatus.Cancelled;
    }

    // ---------- exits (F11 E / G) ----------

    /// <summary>
    /// F11 E: one side (owner or accepted helper) gives up before the work started. The announcement goes back to
    /// <see cref="AnnouncementStatus.Open"/> (other helpers can make offers again), the accepted offer becomes
    /// <see cref="OfferStatus.Abandoned"/>. This helper cannot make another offer here and his conversation becomes
    /// read-only. No ratings – nothing was done. The reason is required (<see cref="JobExitReason.Other"/> with a note).
    /// </summary>
    public JobExit Abandon(UserId actorId, JobExitReason reason, Description? note, DateTime abandonedAtUtc)
    {
        EnsureStatus(AnnouncementStatus.Accepted, "be abandoned");
        EnsureReason(reason, note, JobExitKind.Abandoned);

        var offer = GetOffer(AcceptedOfferId!);
        var exit = RecordExit(JobExitKind.Abandoned, offer, actorId, reason, note, abandonedAtUtc);

        offer.MarkAsAbandoned();
        AcceptedOfferId = null;
        AcceptedAt = null;
        DoneReportedAt = null;
        Status = AnnouncementStatus.Open;

        AddDomainEvent(new JobAbandoned(Id, OwnerId, offer.HelperId, actorId, exit.ActorParty, reason, note?.Value,
            abandonedAtUtc));
        return exit;
    }

    /// <summary>
    /// F11 E: one side (owner or accepted helper) stops the work in progress. Final status
    /// <see cref="AnnouncementStatus.Interrupted"/> – it does not go back to Open; the conversation becomes read-only
    /// and – F11 G – no ratings: the other side can add its statement instead (settlement for the part done is up to them).
    /// </summary>
    public JobExit Interrupt(UserId actorId, JobExitReason reason, Description? note, DateTime interruptedAtUtc)
    {
        EnsureStatus(AnnouncementStatus.InProgress, "be interrupted");
        EnsureReason(reason, note, JobExitKind.Interrupted);

        var offer = GetOffer(AcceptedOfferId!);
        var exit = RecordExit(JobExitKind.Interrupted, offer, actorId, reason, note, interruptedAtUtc);

        Status = AnnouncementStatus.Interrupted;

        AddDomainEvent(new JobInterrupted(Id, OwnerId, offer.HelperId, actorId, exit.ActorParty, reason, note?.Value,
            interruptedAtUtc));
        return exit;
    }

    /// <summary>
    /// F11 G: the other side of an abandonment / interruption gives its own account – once, in any status (the entry is
    /// shown on both users' public profiles). The app does not judge; both versions are visible.
    /// </summary>
    public JobExit RespondToJobExit(int number, UserId actorId, Description response, DateTime respondedAtUtc)
    {
        var exit = _jobExits.FirstOrDefault(e => e.Number == number)
                   ?? throw new DomainRuleException($"There is no job exit number {number} in this announcement.");

        if (actorId != exit.OtherSideOf(OwnerId))
        {
            throw new DomainRuleException("Only the other side of the job can respond to this.");
        }

        if (exit.Response is not null)
        {
            throw new DomainRuleException("The response has already been given.");
        }

        exit.Respond(response, respondedAtUtc);
        AddDomainEvent(new JobExitResponded(Id, OwnerId, exit.HelperId, exit.Number, exit.ActorId, actorId,
            TextPreview.Of(response.Value), respondedAtUtc));
        return exit;
    }

    /// <summary>F11 E: true if a job of this helper on the announcement was abandoned (by either side).</summary>
    public bool HasLeftJob(UserId helperId)
        => _jobExits.Any(e => e.Kind == JobExitKind.Abandoned && e.HelperId == helperId);

    /// <summary>F11 E: the interruption of the job (null unless the status is Interrupted).</summary>
    public JobExit? GetInterruption()
        => _jobExits.FirstOrDefault(e => e.Kind == JobExitKind.Interrupted);

    /// <summary>
    /// F11 E: who can see which exits – like offers (<see cref="GetOffersVisibleTo"/>): the owner all of them, a helper
    /// only those of his own job, anybody else none. Oldest first.
    /// </summary>
    public IEnumerable<JobExit> GetJobExitsVisibleTo(UserId? viewerId)
    {
        if (viewerId is null)
        {
            return [];
        }

        var visible = viewerId == OwnerId ? _jobExits : _jobExits.Where(e => e.HelperId == viewerId);
        return visible.OrderBy(e => e.Number);
    }

    // ---------- rating ----------

    /// <summary>
    /// After completion each participant can rate the other one: the owner rates the helper and the other way round.
    /// Only a completed job (F11 G): after an interruption there are no stars – otherwise two accounts could start and
    /// interrupt fake jobs to collect ratings; the interruption with both statements is shown on the profiles instead.
    /// Rating is a separate aggregate; the "only once" rule is checked by the application layer (and a unique index).
    /// </summary>
    public Rating CreateRating(Id ratingId, UserId raterId, Rate rate, Description? comment, DateTime createdAtUtc)
    {
        EnsureStatus(AnnouncementStatus.Completed, "be rated");
        EnsureParticipant(raterId);

        var rateeId = raterId == OwnerId ? GetAcceptedHelperId()! : OwnerId;

        return new Rating(ratingId, Id, raterId, rateeId, rate, comment, createdAtUtc);
    }

    // ---------- helpers ----------

    // F11 E: only the owner or the accepted helper; the entry remembers on whose side the actor was.
    private JobExit RecordExit(JobExitKind kind, Offer offer, UserId actorId, JobExitReason reason, Description? note,
        DateTime occurredAtUtc)
    {
        EnsureParticipant(actorId);
        var party = actorId == OwnerId ? OfferParty.Client : OfferParty.Helper;

        var exit = new JobExit(Id, _jobExits.Count + 1, kind, offer.Id, offer.HelperId, actorId, party, reason, note,
            occurredAtUtc);
        _jobExits.Add(exit);
        return exit;
    }

    private static void EnsureReason(JobExitReason reason, Description? note, JobExitKind kind)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new InvalidJobExitException($"Reason: {reason} is invalid.");
        }

        // „Konflikt / nie dogadaliśmy się” is a reason to stop the work, not to give up before it started.
        if (reason == JobExitReason.Disagreement && kind == JobExitKind.Abandoned)
        {
            throw new InvalidJobExitException("Disagreement is a reason only for interrupting the work.");
        }

        if (reason == JobExitReason.Other && note is null)
        {
            throw new InvalidJobExitException("Describe the reason when choosing Other.");
        }
    }
}
