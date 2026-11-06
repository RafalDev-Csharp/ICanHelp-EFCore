using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities.Negotiations;

/// <summary>
/// One step of a negotiation about an announcement. The client is always the announcement owner,
/// so an offer only needs to know the helper and who proposed this step.
/// Created and changed only through <see cref="Announcement"/>.
/// </summary>
public class Offer
{
    public Id Id { get; private set; }
    public Id AnnouncementId { get; private set; }

    public UserId HelperId { get; private set; }
    public OfferParty ProposedBy { get; private set; }

    public Price Price { get; private set; }
    public DateTime ProposedStart { get; private set; }
    public DateTime ProposedEnd { get; private set; }
    public Description? Message { get; private set; }

    public OfferStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>The offer this one is a counter-offer to (null for the first offer of a helper).</summary>
    public Id? ParentOfferId { get; private set; }

    /// <summary>
    /// <see cref="Announcement.Revision"/> at the moment the offer was made. If the announcement was edited later,
    /// the offer is outdated: it was priced for a different description, so it cannot be accepted any more.
    /// </summary>
    public int AnnouncementRevision { get; private set; }

    // Required by EF Core.
    private Offer()
    {
    }

    internal Offer(Id id, Id announcementId, UserId helperId, OfferParty proposedBy, Price price,
        DateTime proposedStart, DateTime proposedEnd, Description? message, DateTime createdAtUtc, Id? parentOfferId,
        int announcementRevision)
    {
        if (price.Amount <= 0)
        {
            throw new DomainRuleException("Offer price must be greater than zero.");
        }

        if (proposedEnd <= proposedStart)
        {
            throw new DomainRuleException("Offer end must be later than its start.");
        }

        if (proposedStart < createdAtUtc)
        {
            throw new DomainRuleException("Offer cannot start in the past.");
        }

        Id = id;
        AnnouncementId = announcementId;
        HelperId = helperId;
        ProposedBy = proposedBy;
        Price = price;
        ProposedStart = proposedStart;
        ProposedEnd = proposedEnd;
        Message = message;
        CreatedAt = createdAtUtc;
        ParentOfferId = parentOfferId;
        AnnouncementRevision = announcementRevision;
        Status = OfferStatus.Pending;
    }

    internal void Accept() => Status = OfferStatus.Accepted;

    internal void Reject() => Status = OfferStatus.Rejected;

    internal void Withdraw() => Status = OfferStatus.Withdrawn;

    internal void MarkAsCountered() => Status = OfferStatus.Countered;
}
