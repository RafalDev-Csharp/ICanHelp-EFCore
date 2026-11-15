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
/// <para>
/// One class in four files, by area: this one – the content and its revisions; <c>Announcement.Images.cs</c> – photos;
/// <c>Announcement.Negotiation.cs</c> – offers and the conversation; <c>Announcement.Job.cs</c> – the work after
/// an offer is accepted (start, done, exits, rating).
/// </para>
/// </summary>
public partial class Announcement : AggregateRoot
{
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

    // ---------- content and revisions ----------

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

        // Budget ("150.00 PLN") and Location ("50.0614, 19.9366") – the culture-independent text of the value objects.
        Compare(AnnouncementFields.Category, $"{CategoryId}/{SubcategoryId}", $"{categoryId}/{subcategoryId}");
        Compare(AnnouncementFields.Difficulty, Difficulty.ToString(), difficulty.ToString());
        Compare(AnnouncementFields.Title, Title.Value, title.Value);
        Compare(AnnouncementFields.Description, Description.Value, description.Value);
        Compare(AnnouncementFields.Budget, Budget.ToString(), budget.ToString());
        Compare(AnnouncementFields.Location, Location.ToString(), location.ToString());

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

    // ---------- participants ----------

    /// <summary>The helper whose offer was accepted (null while no offer is accepted).</summary>
    public UserId? GetAcceptedHelperId()
        => AcceptedOfferId is null ? null : GetOffer(AcceptedOfferId).HelperId;

    /// <summary>True if the user is the owner or the helper of the accepted offer.</summary>
    public bool IsParticipant(UserId userId)
        => userId == OwnerId || userId == GetAcceptedHelperId();

    // ---------- rules shared by all areas ----------

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
