using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities;

/// <summary>
/// Aggregate root: a problem published by a user together with the negotiation (offers) about it.
/// All changes of offers go through this class, so the rules are enforced in one place.
/// Other aggregates (user, category) are referenced by id only.
/// </summary>
public class Announcement
{
    public const int MaxImages = 5;

    private readonly List<Offer> _offers = new();
    private readonly List<Image> _images = new();

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

    public IReadOnlyCollection<Offer> Offers => _offers.AsReadOnly();
    public IReadOnlyCollection<Image> Images => _images.AsReadOnly();

    // Required by EF Core.
    private Announcement()
    {
    }

    public Announcement(Id id, UserId ownerId, Id categoryId, Id subcategoryId, DifficultyLevel difficulty,
        StringObject title, Description description, Price budget, Location location, DateTime createdAtUtc)
    {
        EnsureBudget(budget);

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
    }

    public void Update(Id categoryId, Id subcategoryId, DifficultyLevel difficulty, StringObject title,
        Description description, Price budget, Location location)
    {
        EnsureStatus(AnnouncementStatus.Open, "be edited");
        EnsureBudget(budget);

        CategoryId = categoryId;
        SubcategoryId = subcategoryId;
        Difficulty = difficulty;
        Title = title;
        Description = description;
        Budget = budget;
        Location = location;
    }

    // ---------- images ----------

    public Image AddImage(Id imageId, StringObject fileName, StringObject fileExtension, StringObject filePath)
    {
        if (_images.Count >= MaxImages)
        {
            throw new DomainRuleException($"An announcement cannot have more than {MaxImages} images.");
        }

        var image = new Image(imageId, Id, fileName, fileExtension, filePath);
        _images.Add(image);
        return image;
    }

    public void RemoveImage(Id imageId)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId)
            ?? throw new DomainRuleException($"Image with id: {imageId} does not belong to this announcement.");

        _images.Remove(image);
    }

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

        if (_offers.Any(o => o.HelperId == helperId && o.Status == OfferStatus.Pending))
        {
            throw new DomainRuleException("This helper already has a pending offer. Counter or withdraw it instead.");
        }

        var offer = new Offer(offerId, Id, helperId, OfferParty.Helper, price, proposedStart, proposedEnd,
            message, createdAtUtc, parentOfferId: null);

        _offers.Add(offer);
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
        var counter = new Offer(newOfferId, Id, original.HelperId, party, price, proposedStart, proposedEnd,
            message, createdAtUtc, original.Id);

        original.MarkAsCountered();
        _offers.Add(counter);
        return counter;
    }

    /// <summary>The side that did not propose the offer accepts it; all other pending offers are rejected.</summary>
    public void AcceptOffer(Id offerId, UserId actorId)
    {
        EnsureStatus(AnnouncementStatus.Open, "accept an offer");

        var offer = GetPendingOffer(offerId);
        var party = ResolveParty(offer, actorId);

        if (party == offer.ProposedBy)
        {
            throw new DomainRuleException("You cannot accept your own offer.");
        }

        offer.Accept();

        foreach (var other in _offers.Where(o => o.Id != offer.Id && o.Status == OfferStatus.Pending))
        {
            other.Reject();
        }

        AcceptedOfferId = offer.Id;
        Status = AnnouncementStatus.Accepted;
    }

    public void RejectOffer(Id offerId, UserId actorId)
    {
        EnsureStatus(AnnouncementStatus.Open, "reject an offer");

        var offer = GetPendingOffer(offerId);
        var party = ResolveParty(offer, actorId);

        if (party == offer.ProposedBy)
        {
            throw new DomainRuleException("You cannot reject your own offer. Withdraw it instead.");
        }

        offer.Reject();
    }

    public void WithdrawOffer(Id offerId, UserId actorId)
    {
        EnsureStatus(AnnouncementStatus.Open, "have an offer withdrawn");

        var offer = GetPendingOffer(offerId);
        var party = ResolveParty(offer, actorId);

        if (party != offer.ProposedBy)
        {
            throw new DomainRuleException("Only the author of an offer can withdraw it.");
        }

        offer.Withdraw();
    }

    // ---------- lifecycle ----------

    public void Start()
    {
        EnsureStatus(AnnouncementStatus.Accepted, "be started");
        Status = AnnouncementStatus.InProgress;
    }

    public void Complete()
    {
        EnsureStatus(AnnouncementStatus.InProgress, "be completed");
        Status = AnnouncementStatus.Completed;
    }

    public void Cancel()
    {
        if (Status is not (AnnouncementStatus.Open or AnnouncementStatus.Accepted))
        {
            throw new DomainRuleException($"Announcement in status {Status} cannot be cancelled.");
        }

        Status = AnnouncementStatus.Cancelled;
    }

    // ---------- helpers ----------

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

    private void EnsureStatus(AnnouncementStatus expected, string action)
    {
        if (Status != expected)
        {
            throw new DomainRuleException($"Announcement in status {Status} cannot {action}.");
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
