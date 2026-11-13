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
            UpdatedAt = entity.UpdatedAt,
            DoneReportedAt = entity.DoneReportedAt,
            ThumbnailUrl = entity.GetThumbnailUrl()
            // HelperCount: the list queries count it with a subquery (offers are not loaded there).
        };

    /// <summary>URL of the oldest image (needs the images loaded; the list query computes it with a subquery instead).</summary>
    private static string? GetThumbnailUrl(this Announcement entity)
        => entity.GetThumbnail() is { } thumbnail ? ImageDto.GetUrl(entity.Id, thumbnail.Id) : null;

    /// <summary>
    /// Offers only as far as <paramref name="viewerId"/> may see them (Announcement.GetOffersVisibleTo).
    /// <paramref name="isLiked"/> – whether the viewer has liked it (read separately, likes are not part of the aggregate).
    /// </summary>
    public static AnnouncementDetailsDto AsDetailsDto(this Announcement entity, UserId? viewerId, bool isLiked = false)
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
            DoneReportedAt = entity.DoneReportedAt,
            ThumbnailUrl = entity.GetThumbnailUrl(),
            Offers = entity.GetOffersVisibleTo(viewerId).OrderBy(o => o.CreatedAt).Select(o => o.AsDto(entity)).ToList(),
            HelperCount = entity.Offers.Select(o => o.HelperId).Distinct().Count(),
            IsLiked = isLiked,
            Images = entity.Images.OrderBy(i => i.AddedAt).ThenBy(i => i.Id.Value).Select(i => i.AsDto()).ToList(),
            Changes = entity.Changes.OrderBy(c => c.Revision).Select(c => c.AsDto()).ToList(),
            JobExits = entity.GetJobExitsVisibleTo(viewerId).Select(e => e.AsDto()).ToList()
        };

    /// <summary>F11 E: like offers – the owner sees all exits, a helper only those of his own job, others none.</summary>
    private static IEnumerable<JobExit> GetJobExitsVisibleTo(this Announcement entity, UserId? viewerId)
        => viewerId is null
            ? []
            : entity.JobExits
                .Where(e => viewerId == entity.OwnerId || e.HelperId == viewerId)
                .OrderBy(e => e.Number);

    public static JobExitDto AsDto(this JobExit entity)
        => new()
        {
            Number = entity.Number,
            Kind = entity.Kind.ToString(),
            OfferId = entity.OfferId,
            HelperId = entity.HelperId,
            ActorId = entity.ActorId,
            ActorRole = entity.ActorParty.ToString(),
            Reason = entity.Reason.ToString(),
            Note = entity.Note?.Value,
            OccurredAt = entity.OccurredAt,
            Response = entity.Response?.Value,
            RespondedAt = entity.RespondedAt
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

    /// <summary>
    /// The conversation as seen by <paramref name="readerId"/> (a participant). Timeline: messages, offers of this helper
    /// and edits of the announcement made after the conversation started – oldest first.
    /// </summary>
    public static ConversationDto AsDto(this Conversation entity, Announcement announcement, UserId readerId)
    {
        var messages = entity.Messages.Select(m => new ConversationEntryDto
        {
            Type = ConversationEntryDto.MessageType,
            At = m.SentAt,
            Message = m.AsDto(entity)
        });

        var offers = announcement.Offers
            .Where(o => o.HelperId == entity.HelperId)
            .Select(o => new ConversationEntryDto
            {
                Type = ConversationEntryDto.OfferType,
                At = o.CreatedAt,
                Offer = o.AsDto(announcement)
            });

        var changes = announcement.Changes
            .Where(c => c.ChangedAt > entity.CreatedAt)
            .Select(c => new ConversationEntryDto
            {
                Type = ConversationEntryDto.AnnouncementChangedType,
                At = c.ChangedAt,
                Change = c.AsDto()
            });

        var exits = announcement.JobExits
            .Where(e => e.HelperId == entity.HelperId)
            .Select(e => new ConversationEntryDto
            {
                Type = ConversationEntryDto.JobExitType,
                At = e.OccurredAt,
                JobExit = e.AsDto()
            });

        return new ConversationDto
        {
            Id = entity.Id,
            AnnouncementId = entity.AnnouncementId,
            AnnouncementTitle = announcement.Title,
            AnnouncementStatus = announcement.Status.ToString(),
            AnnouncementRevision = announcement.Revision,
            AnnouncementDoneReportedAt = announcement.DoneReportedAt,
            ClientId = entity.ClientId,
            HelperId = entity.HelperId,
            MyRole = entity.GetParty(readerId).ToString(),
            CreatedAt = entity.CreatedAt,
            ClientLastReadAt = entity.ClientLastReadAt,
            HelperLastReadAt = entity.HelperLastReadAt,
            UnreadCount = entity.CountUnread(readerId),
            CanWrite = announcement.AllowsConversationWith(entity.HelperId),
            // Przy tym samym czasie: najpierw zmiana ogłoszenia, potem oferta, potem wiadomość (przyczyna przed reakcją).
            Timeline = changes.Concat(offers).Concat(exits).Concat(messages)
                .OrderBy(e => e.At)
                .ThenBy(e => e.Type switch
                {
                    ConversationEntryDto.AnnouncementChangedType => 0,
                    ConversationEntryDto.OfferType => 1,
                    ConversationEntryDto.JobExitType => 2,
                    _ => 3
                })
                .ToList()
        };
    }

    public static MessageDto AsDto(this ConversationMessage entity, Conversation conversation)
        => new()
        {
            Id = entity.Id,
            SenderId = entity.SenderId,
            SenderRole = conversation.GetParty(entity.SenderId).ToString(),
            Text = entity.Text,
            SentAt = entity.SentAt,
            AnnouncementRevision = entity.AnnouncementRevision
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
            Url = ImageDto.GetUrl(entity.AnnouncementId, entity.Id),
            AddedAt = entity.AddedAt
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
