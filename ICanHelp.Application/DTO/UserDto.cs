using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Entities.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Application.DTO
{
    public class UserDto
    {
        public Guid Id { get; set; }

        public string UserName { get; set; }
        public string FullName { get; set; }

        public Guid AppUserRoleId { get; set; }
        public AppUserRoleDto Role { get; set; }

        public DateTime CreatedAt { get; set; }
        public bool IsLocked { get; set; }

        public string Description { get; set; }

        public Guid ContactDataId { get; set; }
        public ContactDataDto ContactData { get; set; }

        public IEnumerable<AnnouncementDto> Announcements { get; set; }

        public IEnumerable<LikedAnnouncementDto> LikedAnnouncements { get; set; }
        public IEnumerable<LikedUserDto> LikedUsers { get; set; }

        public IEnumerable<RatingDto> ClientRatings { get; set; }
        public IEnumerable<RatingDto> HelperRatings { get; set; }

        public IEnumerable<ClientOfferDto> ClientOffers { get; set; }
        public IEnumerable<HelperOfferDto> HelperOffers { get; set; }
    }
}
