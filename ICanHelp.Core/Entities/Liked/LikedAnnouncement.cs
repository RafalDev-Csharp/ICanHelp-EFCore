using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Entities.Liked
{
    public class LikedAnnouncement : Liked
    {
        public Id AnnouncementId { get; set; }      
        public Announcement Announcement { get; set; }

        public LikedAnnouncement(Id id, bool isLiked, UserId userLikerId, User userLiker, Id announcementId, Announcement announcement) : base(id, isLiked, userLikerId, userLiker)
        {
            AnnouncementId = announcementId;
            AnnouncementId = announcementId;
        }

        public void UpdateIsAnnouncementLiked(bool isLiked)
        {
            IsLiked = isLiked;
        }
    }

}
