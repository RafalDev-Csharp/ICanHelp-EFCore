using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Entities.Negotiations
{
    public abstract class Offer
    {
        public Id Id { get; private set; }

        public Id AnnouncementId { get; set; }  
        public Announcement Announcement { get; set; }

        public UserId ClientId { get; set; }
        public User Client { get; set; }

        public UserId HelperId { get; set; }
        public User Helper { get; set; }

        public CustomDateTime WantToStart { get; set; }
        public CustomDateTime WantToFinish { get; set; }

        public Price Price { get; set; }
        public Description Description { get; set; }

        public UserId AcceptedById { get; set; }
        public AppUserRole AcceptedBy { get; set; }

        public UserId CreatedById { get; set; }
        public AppUserRole CreatedBy { get; set; }

        public bool IsAccepted { get; set; }

        protected Offer(Id id, Id announcementId, UserId clientId, UserId helperId,
            CustomDateTime wantToStart, CustomDateTime wantToFinish, Price price, Description description,
            bool isAccepted, AppUserRole acceptedBy, AppUserRole createdBy)
        {
            Id = id;
            AnnouncementId = announcementId;
            ClientId = clientId;
            HelperId = helperId;

            WantToStart = wantToStart;
            WantToFinish = wantToFinish;
            Price = price;
            Description = description;
            AcceptedBy = acceptedBy;
            CreatedBy = createdBy;
            IsAccepted = isAccepted;
        }
    }
}
