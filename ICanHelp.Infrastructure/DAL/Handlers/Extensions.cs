using ICanHelp.Application.DTO;
using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Entities.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Infrastructure.DAL.Handlers
{
    internal static class Extensions
    {

        public static UserDto AsDto(this User entity)
            => new()
            {
                Id = entity.Id,

                UserName = entity.UserName,
                FullName = entity.FullName,

                AppUserRoleId = entity.AppUserRoleId,
                Role = new AppUserRoleDto
                {
                    Id = entity.Id,
                    UserRoleName = entity.Role?.ToString() ?? string.Empty,
                    ClientOffers = entity.Role?.ClientOffers?.Select(co => co.AsDto()) ?? Enumerable.Empty<ClientOfferDto>(),
                    HelperOffers = entity.Role?.HelperOffers?.Select(ho => ho.AsDto()) ?? Enumerable.Empty<HelperOfferDto>(),
                },

                CreatedAt = entity.CreatedAt,
                IsLocked = entity.IsLocked,
                Description = entity.Description,

                ContactData = entity.ContactData.AsDto(),

                Announcements = entity.Announcements?.Select(a => a.AsDto()) ?? Enumerable.Empty<AnnouncementDto>(),

                LikedAnnouncements = entity.LikedAnnouncements?.Select(la => la.AsDto()) ?? Enumerable.Empty<LikedAnnouncementDto>(),

                LikedUsers = entity.LikedUsers?.Select(lu => lu.AsDto()) ?? Enumerable.Empty<LikedUserDto>(),

                ClientRatings = entity.ClientRatings?.Select(cr => cr.AsDto()) ?? Enumerable.Empty<RatingDto>(),
                HelperRatings = entity.HelperRatings?.Select(hr => hr.AsDto()) ?? Enumerable.Empty<RatingDto>(),

                ClientOffers = entity.ClientOffers?.Select(co => co.AsDto()) ?? Enumerable.Empty<ClientOfferDto>(),

                HelperOffers = entity.HelperOffers?.Select(ho => ho.AsDto()) ?? Enumerable.Empty<HelperOfferDto>(),
            };

        public static ContactDataDto AsDto(this ContactData entity)
            => new()
            {
                Id = entity.Id,
                Email = entity.Email,

                User = entity.User.AsDto(),
                UserId = entity.UserId,

                PhoneNumber = entity.PhoneNumber,
                Country = entity.Country,
                City = entity.City,
                PostalCode = entity.PostalCode,
            };

        public static AnnouncementDto AsDto(this Announcement entity)
            => new()
            {
                Id = entity.Id,

                CategoryId = entity.CategoryId,
                Category = entity.Category.AsDto(),

                SubcategoryId = entity.SubcategoryId,
                Subcategory = entity.Subcategory.AsDto(),

                DifficultLevelId = entity.DifficultLevelId,
                DifficultLevel = entity.DifficultLevel.AsDto(),

                OwnerId = entity.OwnerId,
                User = entity.User.AsDto(),

                Title = entity.Title,
                CreatedAt = entity.CreatedAt,

                LocationId = entity.LocationId,
                Location = entity.Location.AsDto(),

                Description = entity.Description,
                Price = entity.Price,

                IsAvailable = entity.IsAvailable,
                IsFinished = entity.IsFinished,

                ClientOfferId = entity.ClientOfferId,
                ClientOffer = entity.ClientOffer.AsDto(),

                HelperOffers = entity.HelperOffers.Select(ho => ho.AsDto()) ?? Enumerable.Empty<HelperOfferDto>(),

                Images = entity.Images.Select(i => i.AsDto()) ?? Enumerable.Empty<ImageDto>(),
                Ratings = entity.Ratings.Select(r => r.AsDto()) ?? Enumerable.Empty<RatingDto>(),

                LikedAnnouncementId = entity.LikedAnnouncementId,
                LikedAnnouncement = entity.LikedAnnouncement.AsDto(),
            };

        public static LikedAnnouncementDto AsDto(this LikedAnnouncement entity)
            => new()
            {
                Id = entity.Id,

                IsLiked = entity.IsLiked,

                UserLikerId = entity.UserLikerId,
                UserLiker = entity.UserLiker.AsDto(),

                AnnouncementId = entity.AnnouncementId,
                Announcement = entity.Announcement.AsDto(),
            };

        public static LikedUserDto AsDto(this LikedUser entity)
            => new()
            {
                Id = entity.Id,

                IsLiked = entity.IsLiked,

                UserLikerId = entity.UserLikerId,
                UserLiker = entity.UserLiker.AsDto(),

                UserLikedId = entity.UserLikedId,
                UserLiked = entity.UserLiked.AsDto(),
            };

        public static ClientOfferDto AsDto(this ClientOffer entity)
            => new()
            {
                Id = entity.Id,

                AnnouncementId = entity.AnnouncementId,
                Announcement = entity.Announcement.AsDto(),

                ClientId = entity.ClientId,
                Client = entity.Client.AsDto(),

                HelperId = entity.HelperId,
                Helper = entity.Helper.AsDto(),

                WantToStart = entity.WantToStart,
                WantToFinish = entity.WantToFinish,

                Price = entity.Price,
                Description = entity.Description,

                AcceptedById = entity.AcceptedById,
                AcceptedBy = entity.AcceptedBy.AsDto(),

                CreatedById = entity.CreatedById,
                CreatedBy = entity.CreatedBy.AsDto(),

                IsAccepted = entity.IsAccepted,

                RatingHelperId = entity.RatingHelperId,
                RatingHelper = entity.RatingHelper.AsDto(),
            };

        public static HelperOfferDto AsDto(this HelperOffer entity)
            => new()
            {
                Id = entity.Id,

                AnnouncementId = entity.AnnouncementId,
                Announcement = entity.Announcement.AsDto(),

                ClientId = entity.ClientId,
                Client = entity.Client.AsDto(),

                HelperId = entity.HelperId,
                Helper = entity.Helper.AsDto(),

                WantToStart = entity.WantToStart,
                WantToFinish = entity.WantToFinish,

                Price = entity.Price,
                Description = entity.Description,

                AcceptedById = entity.AcceptedById,
                AcceptedBy = entity.AcceptedBy.AsDto(),

                CreatedById = entity.CreatedById,
                CreatedBy = entity.CreatedBy.AsDto(),

                IsAccepted = entity.IsAccepted,

                RatingClientId = entity.RatingClientId,
                RatingClient = entity.RatingClient.AsDto(),
            };



        public static AppUserRoleDto AsDto(this AppUserRole entity)
            => new()
            {
                Id = entity.Id,

                UserRoleName = entity.UserRoleName,

                UserId = entity.UserId,
                User = entity.User.AsDto(),

                ClientOffers = entity.ClientOffers?.Select(co => co.AsDto()) ?? Enumerable.Empty<ClientOfferDto>(),
                HelperOffers = entity.HelperOffers?.Select(ho => ho.AsDto()) ?? Enumerable.Empty<HelperOfferDto>(),
            };
        
        public static CategoryDto AsDto(this Category entity)
            => new()
            {
                Id = entity.Id,

                CategoryName = entity.CategoryName,

                Announcements = entity.Announcements?.Select(a => a.AsDto()) ?? Enumerable.Empty<AnnouncementDto>(),
                Subcategories = entity.Subcategories?.Select(sc => sc.AsDto()) ?? Enumerable.Empty<SubcategoryDto>(),
            };

        public static SubcategoryDto AsDto(this Subcategory entity)
            => new()
            {
                Id = entity.Id,
                
                SubcategoryName = entity.SubcategoryName,

                CategoryId = entity.CategoryId,
                Category = entity.Category.AsDto(),
            };

        public static DifficultLevelDto AsDto(this DifficultLevel entity)
            => new()
            {
                Id = entity.Id,
                
                DifficultLevelName = entity.DifficultLevelName,

                Announcements = entity.Announcements?.Select(a => a.AsDto()) ?? Enumerable.Empty<AnnouncementDto>(),
            };

        public static ImageDto AsDto(this Image entity)
            => new()
            {
                Id = entity.Id,
                
                FileName = entity.FileName,
                FileExtension = entity.FileExtension,
                FilePath = entity.FilePath,

                AnnouncementId = entity.AnnouncementId,
                Announcement = entity.Announcement.AsDto(),
            };

        public static LocationDto AsDto(this Location entity)
            => new()
            {
                Id = entity.Id,
                
                Latitude = entity.Latitude,
                Longitude = entity.Longitude,

                AnnouncementId = entity.AnnouncementId,
                Announcement = entity.Announcement.AsDto(),
            };

        public static RatingDto AsDto(this Rating entity)
            => new()
            {
                Id = entity.Id,

                AnnouncementId = entity.AnnouncementId,
                Announcement = entity.Announcement.AsDto(),

                RaterId = entity.RaterId,
                Rater = entity.Rater.AsDto(),

                RateeId = entity.RateeId,
                Ratee = entity.Ratee.AsDto(),   

                Rate = entity.Rate,
            };
    }
}
