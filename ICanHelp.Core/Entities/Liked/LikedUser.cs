using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Entities.Liked
{
    public class LikedUser : Liked
    {
        public UserId UserLikedId { get; set; }
        public User UserLiked { get; set; }

        public LikedUser(Id id, bool isLiked, UserId userLikerId, User userLiker, UserId userLikedId, User userLiked) : base(id, isLiked, userLikerId, userLiker)
        {
            UserLikedId = userLikedId;
            UserLiked = userLiked;
        }

        public void UpdateIsUserLiked(bool isLiked)
        {
            IsLiked = isLiked;
        }
    }
}
