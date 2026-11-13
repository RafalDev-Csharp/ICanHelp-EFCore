using ICanHelp.Core.Enums;

namespace ICanHelp.Core.Events;

// Every offer event names both sides of the negotiation (owner = client, helper) and who proposed the offer,
// so the handler can pick the recipient (always the other side than the one who acted) without loading anything.

/// <summary>A helper made his first offer.</summary>
public sealed record OfferMade(
    Guid AnnouncementId,
    Guid OfferId,
    Guid OwnerId,
    Guid HelperId,
    decimal Amount,
    string Currency,
    DateTime OccurredAt) : IDomainEvent;

/// <summary><paramref name="ProposedBy"/> answered the other side's offer with a counter-offer (<paramref name="OfferId"/>).</summary>
public sealed record OfferCountered(
    Guid AnnouncementId,
    Guid OfferId,
    Guid ParentOfferId,
    Guid OwnerId,
    Guid HelperId,
    OfferParty ProposedBy,
    decimal Amount,
    string Currency,
    DateTime OccurredAt) : IDomainEvent;

/// <summary>
/// The author (<paramref name="ProposedBy"/>) replaced his outdated offer (<paramref name="ParentOfferId"/>) with new terms
/// for the current version of the announcement (<paramref name="OfferId"/>).
/// </summary>
public sealed record OfferRevised(
    Guid AnnouncementId,
    Guid OfferId,
    Guid ParentOfferId,
    Guid OwnerId,
    Guid HelperId,
    OfferParty ProposedBy,
    decimal Amount,
    string Currency,
    DateTime OccurredAt) : IDomainEvent;

/// <summary>The offer proposed by <paramref name="ProposedBy"/> was accepted by the other side.</summary>
public sealed record OfferAccepted(
    Guid AnnouncementId,
    Guid OfferId,
    Guid OwnerId,
    Guid HelperId,
    OfferParty ProposedBy,
    DateTime OccurredAt) : IDomainEvent;

/// <summary>
/// The offer proposed by <paramref name="ProposedBy"/> was rejected by the other side, or – when
/// <paramref name="AnotherOfferAccepted"/> – automatically, because the owner accepted another helper's offer.
/// </summary>
public sealed record OfferRejected(
    Guid AnnouncementId,
    Guid OfferId,
    Guid OwnerId,
    Guid HelperId,
    OfferParty ProposedBy,
    bool AnotherOfferAccepted,
    DateTime OccurredAt) : IDomainEvent;

/// <summary>The author (<paramref name="ProposedBy"/>) withdrew his offer.</summary>
public sealed record OfferWithdrawn(
    Guid AnnouncementId,
    Guid OfferId,
    Guid OwnerId,
    Guid HelperId,
    OfferParty ProposedBy,
    DateTime OccurredAt) : IDomainEvent;
