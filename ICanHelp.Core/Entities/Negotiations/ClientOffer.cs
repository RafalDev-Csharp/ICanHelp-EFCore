using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Entities.Negotiations
{
    public class ClientOffer : Offer
    {
        public Id RatingHelperId { get; set; }
        public Rating RatingHelper { get; set; }

        public ClientOffer(Id id, Id announcementId, UserId clientId, UserId helperId,
            CustomDateTime wantToStart, CustomDateTime wantToFinish, Price price,
            Description description, bool isAccepted, AppUserRole acceptedBy, AppUserRole createdBy,
            Id ratingHelperId ,Rating ratingHelper)
                : base(id, announcementId, clientId, helperId, wantToStart,
                      wantToFinish, price, description, isAccepted, acceptedBy, createdBy)
        {
            RatingHelperId = ratingHelperId;
            RatingHelper = ratingHelper;
        }

        public void UpdateClientOffer(CustomDateTime wantToStart, CustomDateTime wantToFinish,
            Price price, Description description, bool isAccepted, Id ratingHelperId)
        {
            WantToStart = wantToStart;
            WantToFinish = wantToFinish;
            Price = price;
            Description = description;
            IsAccepted = isAccepted;
            RatingHelperId = ratingHelperId;
        }
    }
}
