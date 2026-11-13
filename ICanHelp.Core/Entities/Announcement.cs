using System.Globalization;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Events;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities;

/// <summary>
/// Aggregate root: a problem published by a user together with the negotiation (offers) about it.
/// All changes of offers go through this class, so the rules are enforced in one place.
/// Other aggregates (user, category) are referenced by id only.
/// Raises domain events (published, edited, offers) – the unit of work saves them to the outbox.
/// </summary>
public class Announcement : AggregateRoot
{
    public const int MaxImages = 5;

    private readonly List<Offer> _offers = new();
    private readonly List<Image> _images = new();
    private readonly List<AnnouncementChange> _changes = new();
    private readonly List<JobExit> _jobExits = new();

    public Id Id { get; private set; }
    public UserId OwnerId { get; private set; }

    public Id CategoryId { get; private set; }
    public Id SubcategoryId { get; private set; }
    public DifficultyLevel Difficulty { get; private set; }

    public StringObject Title { get; private set; }
    public Description Description { get; private set; }

    /// <summary>How much the owner is ready to pay.</summary>
    public Price Budget { get; private set; }
    public Location Location { get; private set; }

    public AnnouncementStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>The offer chosen by the owner (null until an offer is accepted).</summary>
    public Id? AcceptedOfferId { get; private set; }

    /// <summary>
    /// Version of the content: 0 = as published, +1 for every edit that really changes something.
    /// Every offer remembers the revision it was made on (<see cref="Offer.AnnouncementRevision"/>).
    /// Also used as a concurrency token: two simultaneous changes of the same announcement cannot both win.
    /// </summary>
    public int Revision { get; private set; }

    /// <summary>When the content was last edited (null if never).</summary>
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>When the accepted helper reported the work as done (null until then) – the owner confirms it.</summary>
    public DateTime? DoneReportedAt { get; private set; }

    /// <summary>F11 H: when the current offer was accepted (null while open; cleared when the job is abandoned).</summary>
    public DateTime? AcceptedAt { get; private set; }

    /// <summary>F11 H: when the owner confirmed the work (null until <see cref="Complete"/>).</summary>
    public DateTime? CompletedAt { get; private set; }

    public IReadOnlyCollection<Offer> Offers => _offers.AsReadOnly();
    public IReadOnlyCollection<Image> Images => _images.AsReadOnly();

    /// <summary>History of edits, one entry per revision.</summary>
    public IReadOnlyCollection<AnnouncementChange> Changes => _changes.AsReadOnly();

    /// <summary>F11 E: abandonments (before the start) and the interruption (during the work), oldest first.</summary>
    public IReadOnlyCollection<JobExit> JobExits => _jobExits.AsReadOnly();

    // Required by EF Core.
    private Announcement()
    {
    }

    public Announcement(Id id, UserId ownerId, Id categoryId, Id subcategoryId, DifficultyLevel difficulty,
        StringObject title, Description description, Price budget, Location location, DateTime createdAtUtc)
    {
        EnsureBudget(budget);
        EnsureDifficulty(difficulty);

        Id = id;
        OwnerId = ownerId;
        CategoryId = categoryId;
        SubcategoryId = subcategoryId;
        Difficulty = difficulty;
        Title = title;
        Description = description;
        Budget = budget;
        Location = location;
        CreatedAt = createdAtUtc;
        Status = AnnouncementStatus.Open;
        Revision = 0;

        AddDomainEvent(new AnnouncementPublished(Id, OwnerId, CategoryId, SubcategoryId, Location.Latitude,
            Location.Longitude, createdAtUtc));
    }

    /// <summary>
    /// The owner corrects the announcement while it is still open (typo, more details, other budget...).
    /// Returns the parts that really changed. If nothing changed, no new revision is created,
    /// so pending offers do not become outdated without a reason.
    /// </summary>
    public AnnouncementFields Update(UserId actorId, Id categoryId, Id subcategoryId, DifficultyLevel difficulty,
        StringObject title, Description description, Price budget, Location location, DateTime changedAtUtc)
    {
        EnsureStatus(AnnouncementStatus.Open, "be edited");
        EnsureOwner(actorId, "edit");
        EnsureBudget(budget);
        EnsureDifficulty(difficulty);

        // Compared as text – the same text is stored as "before/after", so the UI can show exactly what changed.
        // Even one letter counts: it can change the meaning of the job.
        var fields = new List<AnnouncementFieldChange>();
        void Compare(AnnouncementFields field, string oldValue, string newValue)
        {
            if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
            {
                fields.Add(new AnnouncementFieldChange(field, oldValue, newValue));
            }
        }

        Compare(AnnouncementFields.Category, $"{CategoryId}/{SubcategoryId}", $"{categoryId}/{subcategoryId}");
        Compare(AnnouncementFields.Difficulty, Difficulty.ToString(), difficulty.ToString());
        Compare(AnnouncementFields.Title, Title.Value, title.Value);
        Compare(AnnouncementFields.Description, Description.Value, description.Value);
        Compare(AnnouncementFields.Budget, Format(Budget), Format(budget));
        Compare(AnnouncementFields.Location, Format(Location), Format(location));

        if (fields.Count == 0)
        {
            return AnnouncementFields.None;
        }

        CategoryId = categoryId;
        SubcategoryId = subcategoryId;
        Difficulty = difficulty;
        Title = title;
        Description = description;
        Budget = budget;
        Location = location;

        return RecordChange(fields, changedAtUtc);
    }

    /// <summary>
    /// What is different now compared to the given revision, per field: the value then → the value now.
    /// Several edits of one field are merged (150 → 300 → 500 gives 150 → 500), so a later edit cannot hide
    /// an earlier one: whatever is still different from that revision is listed. A field that was changed and then
    /// changed back to exactly the same text is not listed – there is no difference to show (the edits are still
    /// counted by <see cref="GetEditsSince"/> and kept in <see cref="Changes"/>).
    /// </summary>
    public IReadOnlyList<AnnouncementFieldDiff> GetFieldChangesSince(int revision)
        => _changes
            .Where(c => c.Revision > revision)
            .OrderBy(c => c.Revision)
            .SelectMany(c => c.Fields)
            .GroupBy(f => f.Field)
            .Select(g => new AnnouncementFieldDiff(g.Key, g.First().OldValue, g.Last().NewValue))
            .Where(d => !string.Equals(d.OldValue, d.NewValue, StringComparison.Ordinal))
            .OrderBy(d => d.Field)
            .ToList();

    /// <summary>All parts that are different now compared to the given revision (None if the same).</summary>
    public AnnouncementFields GetChangesSince(int revision)
        => GetFieldChangesSince(revision).Aggregate(AnnouncementFields.None, (all, diff) => all | diff.Field);

    /// <summary>How many edits were made after the given revision (also those that were later reverted).</summary>
    public int GetEditsSince(int revision) => Math.Max(0, Revision - revision);

    // ---------- images ----------

    /// <summary>
    /// The owner adds a photo while the announcement is open. It is a change of the content like any edit:
    /// new revision with the <see cref="AnnouncementFields.Images"/> flag, so pending offers become outdated
    /// (the helper priced the job without seeing this photo).
    /// </summary>
    public Image AddImage(UserId actorId, Id imageId, StringObject fileName, StringObject fileExtension,
        StringObject filePath, DateTime addedAtUtc)
    {
        EnsureStatus(AnnouncementStatus.Open, "have images added");
        EnsureOwner(actorId, "add images to");

        if (_images.Count >= MaxImages)
        {
            throw new DomainRuleException($"An announcement cannot have more than {MaxImages} images.");
        }

        if (_images.Any(i => i.Id == imageId))
        {
            throw new DomainRuleException($"Image with id: {imageId} already belongs to this announcement.");
        }

        var before = FormatImages();
        var image = new Image(imageId, Id, fileName, fileExtension, filePath, addedAtUtc);
        _images.Add(image);

        RecordChange([new AnnouncementFieldChange(AnnouncementFields.Images, before, FormatImages())], addedAtUtc);
        return image;
    }

    /// <summary>
    /// The owner removes a photo while the announcement is open (new revision, like <see cref="AddImage"/>).
    /// Returns the removed image, so the caller can delete its file.
    /// </summary>
    public Image RemoveImage(UserId actorId, Id imageId, DateTime changedAtUtc)
    {
        EnsureStatus(AnnouncementStatus.Open, "have images removed");
        EnsureOwner(actorId, "remove images from");

        var image = _images.FirstOrDefault(i => i.Id == imageId) ?? throw new ImageNotFoundException(imageId);

        var before = FormatImages();
        _images.Remove(image);

        RecordChange([new AnnouncementFieldChange(AnnouncementFields.Images, before, FormatImages())], changedAtUtc);
        return image;
    }

    /// <summary>The thumbnail of the announcement: the oldest image (null if there are no images).</summary>
    public Image? GetThumbnail()
        => _images.OrderBy(i => i.AddedAt).ThenBy(i => i.Id.Value).FirstOrDefault();

    // ---------- negotiation ----------

    /// <summary>A helper proposes to solve the problem for a given price and time.</summary>
    public Offer AddOffer(Id offerId, UserId helperId, Price price, DateTime proposedStart, DateTime proposedEnd,
        Description? message, DateTime createdAtUtc)
    {
        EnsureStatus(AnnouncementStatus.Open, "receive offers");

        if (helperId == OwnerId)
        {
            throw new DomainRuleException("The owner cannot make an offer on his own announcement.");
        }

        if (HasLeftJob(helperId))
        {
            throw new DomainRuleException(
                "This helper's job on the announcement was abandoned – he cannot make another offer on it.");
        }

        if (_offers.Any(o => o.HelperId == helperId && o.Status == OfferStatus.Pending))
        {
            throw new DomainRuleException("This helper already has a pending offer. Counter or withdraw it instead.");
        }

        var offer = new Offer(offerId, Id, helperId, OfferParty.Helper, price, proposedStart, proposedEnd,
            message, createdAtUtc, parentOfferId: null, Revision);

        _offers.Add(offer);
        AddDomainEvent(new OfferMade(Id, offer.Id, OwnerId, helperId, price.Amount, price.Currency, createdAtUtc));
        return offer;
    }

    /// <summary>The other side answers an offer with different terms (price, dates).</summary>
    public Offer CounterOffer(Id offerId, Id newOfferId, UserId actorId, Price price, DateTime proposedStart,
        DateTime proposedEnd, Description? message, DateTime createdAtUtc)
    {
        EnsureStatus(AnnouncementStatus.Open, "receive counter-offers");

        var original = GetPendingOffer(offerId);
        var party = ResolveParty(original, actorId);

        if (party == original.ProposedBy)
        {
            throw new DomainRuleException("You cannot counter your own offer.");
        }

        // The new offer is created first: if its data is invalid, the original stays untouched.
        // A counter-offer is always made on the current revision, so it is also the way to "refresh" an outdated offer.
        var counter = new Offer(newOfferId, Id, original.HelperId, party, price, proposedStart, proposedEnd,
            message, createdAtUtc, original.Id, Revision);

        original.MarkAsCountered();
        _offers.Add(counter);
        AddDomainEvent(new OfferCountered(Id, counter.Id, original.Id, OwnerId, original.HelperId, party,
            price.Amount, price.Currency, createdAtUtc));
        return counter;
    }

    /// <summary>
    /// The author changes his own pending offer before the other side answers it (other price, dates, message) –
    /// also one that became outdated (the announcement was edited after it was made). The new offer is made on the
    /// current revision, the old one becomes <see cref="OfferStatus.Revised"/>. Still one pending offer per helper.
    /// </summary>
    public Offer ReviseOffer(Id offerId, Id newOfferId, UserId actorId, Price price, DateTime proposedStart,
        DateTime proposedEnd, Description? message, DateTime createdAtUtc)
    {
        EnsureStatus(AnnouncementStatus.Open, "have an offer revised");

        var original = GetPendingOffer(offerId);
        var party = ResolveParty(original, actorId);

        if (party != original.ProposedBy)
        {
            throw new DomainRuleException("Only the author of an offer can revise it. Make a counter-offer instead.");
        }

        // As in CounterOffer: the new offer first, so invalid data leaves the original untouched.
        var revised = new Offer(newOfferId, Id, original.HelperId, party, price, proposedStart, proposedEnd,
            message, createdAtUtc, original.Id, Revision);

        original.MarkAsRevised();
        _offers.Add(revised);
        AddDomainEvent(new OfferRevised(Id, revised.Id, original.Id, OwnerId, original.HelperId, party,
            price.Amount, price.Currency, createdAtUtc));
        return revised;
    }

    /// <summary>
    /// The side that did not propose the offer accepts it; all other pending offers are rejected.
    /// An offer made before the last edit of the announcement cannot be accepted – the terms were agreed for
    /// a different description. Either side can make a counter-offer instead (it is made on the current revision).
    /// </summary>
    public void AcceptOffer(Id offerId, UserId actorId, DateTime acceptedAtUtc)
    {
        EnsureStatus(AnnouncementStatus.Open, "accept an offer");

        var offer = GetPendingOffer(offerId);
        var party = ResolveParty(offer, actorId);

        if (party == offer.ProposedBy)
        {
            throw new DomainRuleException("You cannot accept your own offer.");
        }

        if (offer.AnnouncementRevision != Revision)
        {
            throw new DomainRuleException(
                "The announcement has changed since this offer was made. Make a counter-offer with up-to-date terms instead.");
        }

        offer.Accept();
        AddDomainEvent(new OfferAccepted(Id, offer.Id, OwnerId, offer.HelperId, offer.ProposedBy, acceptedAtUtc));

        // ToList: the statuses change in the loop.
        foreach (var other in _offers.Where(o => o.Id != offer.Id && o.Status == OfferStatus.Pending).ToList())
        {
            other.Reject();
            AddDomainEvent(new OfferRejected(Id, other.Id, OwnerId, other.HelperId, other.ProposedBy,
                AnotherOfferAccepted: true, acceptedAtUtc));
        }

        AcceptedOfferId = offer.Id;
        AcceptedAt = acceptedAtUtc;
        Status = AnnouncementStatus.Accepted;
    }

    public void RejectOffer(Id offerId, UserId actorId, DateTime rejectedAtUtc)
    {
        EnsureStatus(AnnouncementStatus.Open, "reject an offer");

        var offer = GetPendingOffer(offerId);
        var party = ResolveParty(offer, actorId);

        if (party == offer.ProposedBy)
        {
            throw new DomainRuleException("You cannot reject your own offer. Withdraw it instead.");
        }

        offer.Reject();
        AddDomainEvent(new OfferRejected(Id, offer.Id, OwnerId, offer.HelperId, offer.ProposedBy,
            AnotherOfferAccepted: false, rejectedAtUtc));
    }

    public void WithdrawOffer(Id offerId, UserId actorId, DateTime withdrawnAtUtc)
    {
        EnsureStatus(AnnouncementStatus.Open, "have an offer withdrawn");

        var offer = GetPendingOffer(offerId);
        var party = ResolveParty(offer, actorId);

        if (party != offer.ProposedBy)
        {
            throw new DomainRuleException("Only the author of an offer can withdraw it.");
        }

        offer.Withdraw();
        AddDomainEvent(new OfferWithdrawn(Id, offer.Id, OwnerId, offer.HelperId, offer.ProposedBy, withdrawnAtUtc));
    }

    /// <summary>
    /// Who can see which offers: the owner – all of them, a helper – only his own negotiation, anybody else – none
    /// (prices and messages of other helpers are not public; only their number is).
    /// </summary>
    public IEnumerable<Offer> GetOffersVisibleTo(UserId? viewerId)
    {
        if (viewerId is null)
        {
            return [];
        }

        return viewerId == OwnerId ? _offers : _offers.Where(o => o.HelperId == viewerId);
    }

    // ---------- conversation ----------

    /// <summary>
    /// A helper opens a conversation with the owner – e.g. to ask a question before making an offer
    /// (making an offer opens it automatically). Only while the announcement is open, never by the owner.
    /// "One conversation per helper" is checked by the application layer (and a unique index).
    /// </summary>
    public Conversation StartConversation(Id conversationId, UserId helperId, DateTime createdAtUtc)
    {
        EnsureStatus(AnnouncementStatus.Open, "start a conversation");

        if (helperId == OwnerId)
        {
            throw new DomainRuleException("The owner cannot start a conversation on his own announcement.");
        }

        if (HasLeftJob(helperId))
        {
            throw new DomainRuleException("This helper's job on the announcement was abandoned.");
        }

        return new Conversation(conversationId, Id, OwnerId, helperId, createdAtUtc);
    }

    /// <summary>
    /// Can the owner and this helper (still) write to each other? While open – every helper, except the one whose
    /// job was abandoned (F11 E: his conversation becomes read-only). After acceptance only the accepted helper, until
    /// the job is done (details of the job). Completed / Interrupted – nobody: the conversation becomes read-only, what
    /// is left is rating each other. Cancelled – nobody.
    /// </summary>
    public bool AllowsConversationWith(UserId helperId)
        => Status switch
        {
            AnnouncementStatus.Open => !HasLeftJob(helperId),
            AnnouncementStatus.Accepted or AnnouncementStatus.InProgress => helperId == GetAcceptedHelperId(),
            _ => false
        };

    // ---------- lifecycle ----------

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
            MyConversationPreview(response.Value), respondedAtUtc));
        return exit;
    }

    // Short preview for the notification (like a message preview).
    private static string MyConversationPreview(string text)
        => text.Length <= 120 ? text : string.Concat(text.AsSpan(0, 119), "…");

    /// <summary>F11 E: true if a job of this helper on the announcement was abandoned (by either side).</summary>
    public bool HasLeftJob(UserId helperId)
        => _jobExits.Any(e => e.Kind == JobExitKind.Abandoned && e.HelperId == helperId);

    /// <summary>F11 E: the interruption of the job (null unless the status is Interrupted).</summary>
    public JobExit? GetInterruption()
        => _jobExits.FirstOrDefault(e => e.Kind == JobExitKind.Interrupted);

    public void Cancel(UserId actorId)
    {
        if (Status is not (AnnouncementStatus.Open or AnnouncementStatus.Accepted))
        {
            throw new DomainRuleException($"Announcement in status {Status} cannot be cancelled.");
        }

        EnsureOwner(actorId, "cancel");

        Status = AnnouncementStatus.Cancelled;
    }

    /// <summary>The helper whose offer was accepted (null while no offer is accepted).</summary>
    public UserId? GetAcceptedHelperId()
        => AcceptedOfferId is null ? null : GetOffer(AcceptedOfferId).HelperId;

    /// <summary>True if the user is the owner or the helper of the accepted offer.</summary>
    public bool IsParticipant(UserId userId)
        => userId == OwnerId || userId == GetAcceptedHelperId();

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

    // Every real change of the content (edit, image added/removed): new revision + history entry + event.
    private AnnouncementFields RecordChange(IReadOnlyCollection<AnnouncementFieldChange> fields, DateTime changedAtUtc)
    {
        Revision++;
        UpdatedAt = changedAtUtc;
        var change = new AnnouncementChange(Id, Revision, changedAtUtc, fields);
        _changes.Add(change);
        AddDomainEvent(new AnnouncementEdited(Id, OwnerId, Revision, change.ChangedFields, changedAtUtc));
        return change.ChangedFields;
    }

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

    private static string Format(Price price)
        => $"{price.Amount.ToString("0.00", CultureInfo.InvariantCulture)} {price.Currency}";

    // Images as "before/after" text: ids in the order they were added, e.g. "id1, id2". Ids (not file names), because
    // two different photos can have the same name – the UI compares both lists: new id = added, missing id = removed.
    // Max 5 × 36 chars, fits the 500-char column.
    private string FormatImages()
        => string.Join(", ", _images.OrderBy(i => i.AddedAt).ThenBy(i => i.Id.Value).Select(i => i.Id.Value.ToString()));

    private static string Format(Location location)
        => string.Create(CultureInfo.InvariantCulture, $"{location.Latitude}, {location.Longitude}");

    private Offer GetOffer(Id offerId)
        => _offers.FirstOrDefault(o => o.Id == offerId) ?? throw new OfferNotFoundException(offerId);

    private Offer GetPendingOffer(Id offerId)
    {
        var offer = GetOffer(offerId);

        if (offer.Status != OfferStatus.Pending)
        {
            throw new DomainRuleException($"Offer in status {offer.Status} cannot be changed.");
        }

        return offer;
    }

    // Decides on whose side the user acts in the negotiation: the owner is the client, the offer's helper is the helper.
    private OfferParty ResolveParty(Offer offer, UserId actorId)
    {
        if (actorId == OwnerId)
        {
            return OfferParty.Client;
        }

        if (actorId == offer.HelperId)
        {
            return OfferParty.Helper;
        }

        throw new DomainRuleException("The user is not a participant of this negotiation.");
    }

    private void EnsureParticipant(UserId actorId)
    {
        if (!IsParticipant(actorId))
        {
            throw new DomainRuleException("Only the owner or the accepted helper can do this.");
        }
    }

    private UserId EnsureAcceptedHelper(UserId actorId, string action)
    {
        var helperId = GetAcceptedHelperId();
        if (helperId is null || actorId != helperId)
        {
            throw new DomainRuleException($"Only the accepted helper can {action} the work.");
        }

        return helperId;
    }

    private void EnsureOwner(UserId actorId, string action)
    {
        if (actorId != OwnerId)
        {
            throw new DomainRuleException($"Only the owner can {action} the announcement.");
        }
    }

    private void EnsureStatus(AnnouncementStatus expected, string action)
    {
        if (Status != expected)
        {
            throw new DomainRuleException($"Announcement in status {Status} cannot {action}.");
        }
    }

    private static void EnsureDifficulty(DifficultyLevel difficulty)
    {
        if (!Enum.IsDefined(difficulty))
        {
            throw new DomainRuleException($"Difficulty: {difficulty} is invalid.");
        }
    }

    private static void EnsureBudget(Price budget)
    {
        if (budget.Amount <= 0)
        {
            throw new DomainRuleException("Budget must be greater than zero.");
        }
    }
}
