using ICanHelp.Core.Enums;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities;

/// <summary>
/// F11 E: one side left an accepted job – before the start (<see cref="JobExitKind.Abandoned"/>) or during the work
/// (<see cref="JobExitKind.Interrupted"/>). Who, when, at which stage and why – shown in the history of the announcement,
/// in the conversation and counted on the user's profile. Created only through <see cref="Announcement"/>.
/// </summary>
public class JobExit
{
    public Id AnnouncementId { get; private set; }

    /// <summary>1 for the first exit of the announcement, 2 for the next... (an abandoned announcement can be accepted again).</summary>
    public int Number { get; private set; }

    public JobExitKind Kind { get; private set; }

    /// <summary>The accepted offer the job was based on.</summary>
    public Id OfferId { get; private set; }

    /// <summary>The helper of that offer (also when the owner was the one who left).</summary>
    public UserId HelperId { get; private set; }

    /// <summary>Who left: the owner (Client) or the helper.</summary>
    public UserId ActorId { get; private set; }
    public OfferParty ActorParty { get; private set; }

    public JobExitReason Reason { get; private set; }
    public Description? Note { get; private set; }
    public DateTime OccurredAt { get; private set; }

    /// <summary>F11 G: the statement of the other side (the one who did not leave) – once, null until given.</summary>
    public Description? Response { get; private set; }
    public DateTime? RespondedAt { get; private set; }

    /// <summary>The user who may answer: the owner when the helper left, the helper when the owner left.</summary>
    public UserId OtherSideOf(UserId ownerId) => ActorParty == OfferParty.Client ? HelperId : ownerId;

    // Required by EF Core.
    private JobExit()
    {
    }

    internal JobExit(Id announcementId, int number, JobExitKind kind, Id offerId, UserId helperId, UserId actorId,
        OfferParty actorParty, JobExitReason reason, Description? note, DateTime occurredAtUtc)
    {
        AnnouncementId = announcementId;
        Number = number;
        Kind = kind;
        OfferId = offerId;
        HelperId = helperId;
        ActorId = actorId;
        ActorParty = actorParty;
        Reason = reason;
        Note = note;
        OccurredAt = occurredAtUtc;
    }

    internal void Respond(Description response, DateTime respondedAtUtc)
    {
        Response = response;
        RespondedAt = respondedAtUtc;
    }
}
