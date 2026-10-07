using ICanHelp.Core.Entities.Liked;
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
    public class Announcement
    {
        public Id Id { get; set; }

        public Id CategoryId { get; set; }
        public Category Category { get; set; }

        public Id SubcategoryId { get; set; }
        public Subcategory Subcategory { get; set; }

        public Id DifficultLevelId { get; set; }
        public DifficultLevel DifficultLevel { get; set; }

        public UserId OwnerId { get; set; }
        public User User { get; set; }

        public StringObject Title { get; set; }
        public CustomDateTime CreatedAt { get; set; }

        public Id LocationId { get; set; }
        public Location Location { get; set; }

        public Description Description { get; set; }
        public Price Price { get; set; }
        public bool IsAvailable { get; set; }
        public bool IsFinished { get; set; }


        public Id ClientOfferId { get; set; }
        public ClientOffer ClientOffer { get; set; }

        public IEnumerable<HelperOffer> HelperOffers { get; set; }

        public IEnumerable<Image> Images { get; set; }
        public IEnumerable<Rating> Ratings { get; set; }

        public Id LikedAnnouncementId { get; set; }
        public LikedAnnouncement LikedAnnouncement { get; set; }



        public Announcement(Id id, Category category, Id categoryId, Subcategory subcategory, Id subcategoryId, DifficultLevel difficultLevel, Id difficultLevelId, UserId ownerId, StringObject title, CustomDateTime createdAt,
            Location location, Description description, Price price, bool isAvailable, bool isFinished,
            ClientOffer clientOffer, IEnumerable<HelperOffer> helperOffers, IEnumerable<Image> images, IEnumerable<Rating> ratings, Id likedAnnouncementId, LikedAnnouncement likedAnnouncement)
        {
            Id = id;
            Category = category;
            CategoryId = categoryId;
            Subcategory = subcategory;
            SubcategoryId = subcategoryId;
            DifficultLevel = difficultLevel;
            DifficultLevelId = difficultLevelId;
            OwnerId = ownerId;
            Title = title;
            CreatedAt = createdAt;
            Location = location;
            Description = description;
            Price = price;
            IsAvailable = isAvailable;
            IsFinished = isFinished;
            ClientOffer = clientOffer;
            HelperOffers = helperOffers;
            Images = images;
            Ratings = ratings;
            LikedAnnouncementId = likedAnnouncementId;
            LikedAnnouncement = likedAnnouncement;
        }

        public void UpdateAnnouncement(Category category, Subcategory subcategory,
            DifficultLevel difficultLevel, StringObject title, Location location,
            Description description, Price price, bool isAvailable, bool isFinished)
        {
            Category = category;
            Subcategory = subcategory;
            DifficultLevel = difficultLevel;
            Title = title;
            Location = location;
            Description = description;
            Price = price;
            IsAvailable = isAvailable;
            IsFinished = isFinished;
        }
    }
}
