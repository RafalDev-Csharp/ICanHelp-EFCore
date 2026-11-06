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
            AcceptedOfferId = entity.AcceptedOfferId?.Value,
            Revision = entity.Revision,
            UpdatedAt = entity.UpdatedAt
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
            Revision = entity.Revision,
            UpdatedAt = entity.UpdatedAt,
            Offers = entity.Offers.OrderBy(o => o.CreatedAt).Select(o => o.AsDto(entity)).ToList(),
            Images = entity.Images.Select(i => i.AsDto()).ToList(),
            Changes = entity.Changes.OrderBy(c => c.Revision).Select(c => c.AsDto()).ToList()
        };

    /// <summary>The announcement is needed to tell whether it changed after the offer (warning in the UI).</summary>
    public static OfferDto AsDto(this Offer entity, Announcement announcement)
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
            ParentOfferId = entity.ParentOfferId?.Value,
            AnnouncementRevision = entity.AnnouncementRevision,
            IsOutdated = entity.AnnouncementRevision != announcement.Revision,
            EditsSinceOffer = announcement.GetEditsSince(entity.AnnouncementRevision),
            ChangesSinceOffer = announcement.GetFieldChangesSince(entity.AnnouncementRevision)
                .Select(d => new AnnouncementFieldChangeDto
                {
                    Field = d.Field.ToString(),
                    OldValue = d.OldValue,
                    NewValue = d.NewValue
                })
                .ToList()
        };

    public static AnnouncementChangeDto AsDto(this AnnouncementChange entity)
        => new()
        {
            Revision = entity.Revision,
            ChangedAt = entity.ChangedAt,
            Fields = entity.Fields.OrderBy(f => f.Field).Select(f => f.AsDto()).ToList()
        };

    public static AnnouncementFieldChangeDto AsDto(this AnnouncementFieldChange entity)
        => new() { Field = entity.Field.ToString(), OldValue = entity.OldValue, NewValue = entity.NewValue };

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
