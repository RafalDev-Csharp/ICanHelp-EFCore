using ICanHelp.Application.DTO;
using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Infrastructure.DAL.Handlers;

/// <summary>
/// Simple entity → DTO mappings: categories, users, ratings, likes and shared value objects.
/// Announcements: <see cref="AnnouncementMapping"/>, conversations: <see cref="ConversationMapping"/>.
/// </summary>
internal static class DtoMapping
{
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
}
