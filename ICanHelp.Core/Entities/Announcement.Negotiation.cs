using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Events;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities;

// Negotiation while the announcement is open: offers of the helpers, answers of the other side, the conversation.
public partial class Announcement
{
    /// <summary>A helper proposes to solve the problem for a given price and time.</summary>
    public Offer AddOffer(Id offerId, UserId helperId, OfferTerms terms, DateTime createdAtUtc)
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

        var offer = new Offer(offerId, Id, helperId, OfferParty.Helper, terms, createdAtUtc, parentOfferId: null,
            Revision);

        _offers.Add(offer);
        AddDomainEvent(new OfferMade(Id, offer.Id, OwnerId, helperId, offer.Price.Amount, offer.Price.Currency,
            createdAtUtc));
        return offer;
    }

    /// <summary>The other side answers an offer with different terms (price, dates).</summary>
    public Offer CounterOffer(Id offerId, Id newOfferId, UserId actorId, OfferTerms terms, DateTime createdAtUtc)
    {
        EnsureStatus(AnnouncementStatus.Open, "receive counter-offers");

        var original = GetPendingOffer(offerId);
        var party = ResolveParty(original, actorId);

        if (party == original.ProposedBy)
        {
            throw new DomainRuleException("You cannot counter your own offer.");
        }

        // A counter-offer is always made on the current revision, so it is also the way to "refresh" an outdated offer.
        var counter = AddNextOffer(original, newOfferId, party, terms, createdAtUtc);
        original.MarkAsCountered();

        AddDomainEvent(new OfferCountered(Id, counter.Id, original.Id, OwnerId, original.HelperId, party,
            counter.Price.Amount, counter.Price.Currency, createdAtUtc));
        return counter;
    }

    /// <summary>
    /// The author changes his own pending offer before the other side answers it (other price, dates, message) –
    /// also one that became outdated (the announcement was edited after it was made). The new offer is made on the
    /// current revision, the old one becomes <see cref="OfferStatus.Revised"/>. Still one pending offer per helper.
    /// </summary>
    public Offer ReviseOffer(Id offerId, Id newOfferId, UserId actorId, OfferTerms terms, DateTime createdAtUtc)
    {
        EnsureStatus(AnnouncementStatus.Open, "have an offer revised");

        var original = GetPendingOffer(offerId);
        var party = ResolveParty(original, actorId);

        if (party != original.ProposedBy)
        {
            throw new DomainRuleException("Only the author of an offer can revise it. Make a counter-offer instead.");
        }

        var revised = AddNextOffer(original, newOfferId, party, terms, createdAtUtc);
        original.MarkAsRevised();

        AddDomainEvent(new OfferRevised(Id, revised.Id, original.Id, OwnerId, original.HelperId, party,
            revised.Price.Amount, revised.Price.Currency, createdAtUtc));
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

    // ---------- helpers ----------

    // The next step of the same negotiation: an offer of the same helper, made on the current revision and pointing at
    // the one it replaces. It is created before the original changes its status – invalid terms leave everything untouched.
    private Offer AddNextOffer(Offer original, Id newOfferId, OfferParty proposedBy, OfferTerms terms,
        DateTime createdAtUtc)
    {
        var next = new Offer(newOfferId, Id, original.HelperId, proposedBy, terms, createdAtUtc, original.Id, Revision);
        _offers.Add(next);
        return next;
    }

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
}
