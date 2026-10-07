using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Application.DTO
{
    public class RatingDto
    {
        public Guid Id { get; set; }

        public Guid AnnouncementId { get; set; }
        public AnnouncementDto Announcement { get; set; }

        public Guid RateeId { get; set; }
        public UserDto Ratee { get; set; }

        public Guid RaterId { get; set; }
        public UserDto Rater { get; set; }

        public double Rate { get; set; }

        public Guid ClientOfferId { get; set; }
        public ClientOfferDto ClientOffer { get; set; }

        public Guid HelperOfferId { get; set; }
        public HelperOfferDto HelperOffer { get; set; }
    }
}
