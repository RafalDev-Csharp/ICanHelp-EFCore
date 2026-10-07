using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Entities
{
    public class Rating
    {
        public Id Id { get; set; }

        public Id AnnouncementId { get; set; }
        public Announcement Announcement { get; set; }

        public UserId RateeId { get; set; }
        public User Ratee { get; set; }

        public UserId RaterId { get; set; }
        public User Rater { get; set; }

        public Rate Rate { get; set; }

        public Id ClientOfferId { get; set; }
        public ClientOffer ClientOffer { get; set; }

        public Id HelperOfferId { get; set; }
        public HelperOffer HelperOffer { get; set; }



        protected Rating(Id id, Id announcementId, UserId rateeId, UserId raterId, Rate rate, Id clientOfferId, Id helperOfferId)
        {
            Id = id;
            AnnouncementId = announcementId;
            RaterId = raterId;
            RateeId = rateeId;
            Rate = rate;
            ClientOfferId = clientOfferId;
            HelperOfferId = helperOfferId;
        }

        public void UpdateRate(Rate rate)
        {
            Rate = rate;
        }
    }
}
