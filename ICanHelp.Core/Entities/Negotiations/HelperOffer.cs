using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Entities.Negotiations
{
    public class HelperOffer : Offer
    {
        public Id RatingClientId { get; set; }
        public Rating RatingClient { get; set; }
        
        public HelperOffer(Id id, Id announcementId, UserId clientId, UserId helperId,
            CustomDateTime wantToStart, CustomDateTime wantToFinish, Price price,
            Description description, bool isAccepted, AppUserRole acceptedBy, AppUserRole createdBy,
            Id ratingClientId, Rating ratingClient)
                : base(id, announcementId, clientId, helperId, wantToStart,
                      wantToFinish, price, description, isAccepted, acceptedBy, createdBy)
        {
            RatingClientId = ratingClientId;
            RatingClient = ratingClient;
        }

        public void UpdateHelperOffer(CustomDateTime wantToStart, CustomDateTime wantToFinish,
            Price price, Description description, bool isAccepted, Rating ratingClient)
        {
            WantToStart = wantToStart;
            WantToFinish = wantToFinish;
            Price = price;
            Description = description;
            IsAccepted = isAccepted;
            RatingClient = ratingClient;
        }
    }
}
