using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Entities.Users
{
    public class User
    {
        public UserId Id { get; private set; }

        public UserName UserName { get; private set; }
        public Password Password { get; private set; }
        public FullName FullName { get; private set; }

        public AppUserRole Role { get; private set; }
        public Id AppUserRoleId { get; private set; }

        public CustomDateTime CreatedAt { get; private set; }
        public bool IsLocked { get; private set; }        
        public Description Description { get; private set; }

        public Id ContactDataId { get; set; }
        public ContactData ContactData { get; private set; }

        public IEnumerable<Announcement> Announcements { get; set; }

        public IEnumerable<LikedAnnouncement> LikedAnnouncements { get; set; }
        public IEnumerable<LikedUser> LikedUsers { get; set; }

        public IEnumerable<Rating> ClientRatings { get; set; }
        public IEnumerable<Rating> HelperRatings { get; set; }

        public IEnumerable<ClientOffer> ClientOffers { get; set; }
        public IEnumerable<HelperOffer> HelperOffers { get; set; }



        public User(UserId id, UserName userName, Password password, FullName fullName, AppUserRole role, Id appUserRoleId,
            CustomDateTime createdAt, bool isLocked, Description description, ContactData contactData,
            IEnumerable<Announcement> announcements, IEnumerable<LikedAnnouncement> likedAnnouncements,
            IEnumerable<LikedUser> likedUsers, IEnumerable<Rating> clientRatings, IEnumerable<Rating> helperRatings,
            IEnumerable<ClientOffer> clientOffers, IEnumerable<HelperOffer> helperOffers)
        {
            Id = id;
            UserName = userName;
            Password = password;
            FullName = fullName;
            Role = role;
            AppUserRoleId = appUserRoleId;
            CreatedAt = createdAt;
            IsLocked = isLocked;
            Description = description;
            ContactData = contactData;
            Announcements = announcements;
            LikedAnnouncements = likedAnnouncements;
            LikedUsers = likedUsers;
            ClientRatings = clientRatings;
            HelperRatings = helperRatings;
            ClientOffers = clientOffers;
            HelperOffers = helperOffers;
        }

        public void UpdateUser(UserName userName, FullName fullName, AppUserRole role, Id appUserRoleId, bool isLocked,
            Description description, ContactData contactData)
        {
            UserName = userName;
            FullName = fullName;
            Role = role;
            AppUserRoleId = appUserRoleId;
            IsLocked = isLocked;
            Description = description;
            ContactData = contactData;
        }
    }
}
