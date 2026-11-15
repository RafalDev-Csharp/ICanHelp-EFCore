namespace ICanHelp.Core.Exceptions;

public class OfferNotFoundException : NotFoundException
{
    public Guid OfferId { get; }

    public OfferNotFoundException(Guid offerId) : base($"Offer with id: {offerId} was not found.")
    {
        OfferId = offerId;
    }
}
