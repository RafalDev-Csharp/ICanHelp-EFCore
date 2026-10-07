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
    public class LikedAnnouncementDto
    {
        public Guid Id { get; set; }

        public bool IsLiked { get; set; }

        public Guid UserLikerId { get; set; }
        public UserDto UserLiker { get; set; }

        public Guid AnnouncementId { get; set; }
        public AnnouncementDto Announcement { get; set; }
    }
}
