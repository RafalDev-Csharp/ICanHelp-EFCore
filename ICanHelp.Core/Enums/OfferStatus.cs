namespace ICanHelp.Core.Enums;

public enum OfferStatus
{
    Pending = 1,
    Accepted = 2,
    Rejected = 3,

    /// <summary>Replaced by a counter-offer from the other side.</summary>
    Countered = 4,

    /// <summary>Taken back by its author.</summary>
    Withdrawn = 5
}
