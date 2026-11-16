namespace ICanHelp.Core.ValueObjects;

/// <summary>
/// What one side proposes in a negotiation: the price, when the work would be done and an optional message.
/// The same set of values for a first offer, a counter-offer and a revised offer. The rules (price above zero,
/// end after start, not in the past) are checked when the offer is created (<c>Offer</c>).
/// </summary>
public sealed record OfferTerms(Price Price, DateTime ProposedStart, DateTime ProposedEnd, Description? Message = null);
