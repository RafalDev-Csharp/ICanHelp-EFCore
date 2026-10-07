using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Application.DTO
{
    public class ClientOfferDto
    {
        public Guid Id { get; set; }

        public Guid AnnouncementId { get; set; }
        public AnnouncementDto Announcement { get; set; }

        public Guid ClientId { get; set; }
        public UserDto Client { get; set; }

        public Guid HelperId { get; set; }
        public UserDto Helper { get; set; }

        public DateTime WantToStart { get; set; }
        public DateTime WantToFinish { get; set; }

        public decimal Price { get; set; }
        public string Description { get; set; }

        public Guid AcceptedById { get; set; }
        public AppUserRoleDto AcceptedBy { get; set; }

        public Guid CreatedById { get; set; }
        public AppUserRoleDto CreatedBy { get; set; }

        public bool IsAccepted { get; set; }

        public Guid RatingHelperId { get; set; }
        public RatingDto RatingHelper { get; set; }
    }
}
