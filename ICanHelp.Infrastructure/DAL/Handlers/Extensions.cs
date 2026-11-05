using ICanHelp.Application.DTO;
using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Infrastructure.DAL.Handlers;

/// <summary>Mapping from domain entities to DTOs returned by the query handlers.</summary>
internal static class Extensions
{
    public static AnnouncementDto AsDto(this Announcement entity)
        => new()
        {
            Id = entity.Id,
            OwnerId = entity.OwnerId,
            CategoryId = entity.CategoryId,
            SubcategoryId = entity.SubcategoryId,
            Difficulty = entity.Difficulty.ToString(),
            Title = entity.Title,
            Description = entity.Description,
            Budget = entity.Budget.AsDto(),
            Location = entity.Location.AsDto(),
            Status = entity.Status.ToString(),
            CreatedAt = entity.CreatedAt,
            AcceptedOfferId = entity.AcceptedOfferId?.Value
        };

    public static AnnouncementDetailsDto AsDetailsDto(this Announcement entity)
        => new()
        {
            Id = entity.Id,
            OwnerId = entity.OwnerId,
            CategoryId = entity.CategoryId,
            SubcategoryId = entity.SubcategoryId,
            Difficulty = entity.Difficulty.ToString(),
            Title = entity.Title,
            Description = entity.Description,
            Budget = entity.Budget.AsDto(),
            Location = entity.Location.AsDto(),
            Status = entity.Status.ToString(),
            CreatedAt = entity.CreatedAt,
            AcceptedOfferId = entity.AcceptedOfferId?.Value,
            Offers = entity.Offers.OrderBy(o => o.CreatedAt).Select(o => o.AsDto()).ToList(),
            Images = entity.Images.Select(i => i.AsDto()).ToList()
        };

    public static OfferDto AsDto(this Offer entity)
        => new()
        {
            Id = entity.Id,
            AnnouncementId = entity.AnnouncementId,
            HelperId = entity.HelperId,
            ProposedBy = entity.ProposedBy.ToString(),
            Price = entity.Price.AsDto(),
            ProposedStart = entity.ProposedStart,
            ProposedEnd = entity.ProposedEnd,
            Message = entity.Message?.Value,
            Status = entity.Status.ToString(),
            CreatedAt = entity.CreatedAt,
            ParentOfferId = entity.ParentOfferId?.Value
        };

    public static ImageDto AsDto(this Image entity)
        => new()
        {
            Id = entity.Id,
            AnnouncementId = entity.AnnouncementId,
            FileName = entity.FileName,
            FileExtension = entity.FileExtension,
            FilePath = entity.FilePath
        };

    public static CategoryDto AsDto(this Category entity)
        => new()
        {
            Id = entity.Id,
            Name = entity.CategoryName,
            Subcategories = entity.Subcategories
                .OrderBy(s => s.SubcategoryName.Value)
                .Select(s => s.AsDto())
                .ToList()
        };

    public static SubcategoryDto AsDto(this Subcategory entity)
        => new()
        {
            Id = entity.Id,
            CategoryId = entity.CategoryId,
            Name = entity.SubcategoryName
        };

    public static UserDto AsDto(this User entity)
        => new()
        {
            Id = entity.Id,
            UserName = entity.UserName,
            FullName = entity.FullName,
            Role = entity.Role.ToString(),
            CreatedAt = entity.CreatedAt,
            IsLocked = entity.IsLocked,
            Description = entity.Description?.Value,
            ContactData = entity.ContactData.AsDto()
        };

    public static UserPublicDto AsPublicDto(this User entity)
        => new()
        {
            Id = entity.Id,
            UserName = entity.UserName,
            FullName = entity.FullName,
            Description = entity.Description?.Value,
            City = entity.ContactData.City,
            CreatedAt = entity.CreatedAt
        };

    public static ContactDataDto AsDto(this ContactData entity)
        => new()
        {
            Email = entity.Email,
            PhoneNumber = entity.PhoneNumber,
            Country = entity.Country,
            City = entity.City,
            PostalCode = entity.PostalCode
        };

    public static RatingDto AsDto(this Rating entity)
        => new()
        {
            Id = entity.Id,
            AnnouncementId = entity.AnnouncementId,
            RaterId = entity.RaterId,
            RateeId = entity.RateeId,
            Rate = entity.Rate,
            Comment = entity.Comment?.Value,
            CreatedAt = entity.CreatedAt
        };

    public static LikedAnnouncementDto AsDto(this LikedAnnouncement entity)
        => new() { UserId = entity.UserId, AnnouncementId = entity.AnnouncementId, LikedAt = entity.LikedAt };

    public static LikedUserDto AsDto(this LikedUser entity)
        => new() { LikerId = entity.LikerId, LikedId = entity.LikedId, LikedAt = entity.LikedAt };

    public static PriceDto AsDto(this Price price)
        => new() { Amount = price.Amount, Currency = price.Currency };

    public static LocationDto AsDto(this Location location)
        => new() { Latitude = location.Latitude, Longitude = location.Longitude };

    /// <summary>Keeps paging values in a safe range (page from 1, size 1-100).</summary>
    public static (int Skip, int Take) ToSkipTake(int page, int pageSize)
    {
        var take = Math.Clamp(pageSize, 1, 100);
        var skip = (Math.Max(page, 1) - 1) * take;
        return (skip, take);
    }
}
