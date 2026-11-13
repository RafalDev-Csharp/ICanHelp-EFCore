namespace ICanHelp.Core.Enums;

public enum OfferStatus
{
    Pending = 1,
    Accepted = 2,
    Rejected = 3,

    /// <summary>Replaced by a counter-offer from the other side.</summary>
    Countered = 4,

    /// <summary>Taken back by its author.</summary>
    Withdrawn = 5,

    /// <summary>Outdated offer replaced by its author with new terms for the current version of the announcement.</summary>
    Revised = 6,

    /// <summary>F11 E: the accepted offer whose job one side abandoned before the start („Zerwana”).</summary>
    Abandoned = 7
}
