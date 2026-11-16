using ICanHelp.Application.DTO;
using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Infrastructure.DAL.Handlers;

/// <summary>An announcement and the parts of its aggregate (offers, images, edits, job exits) → DTOs.</summary>
internal static class AnnouncementMapping
{
    /// <summary>
    /// The announcement as on a list. HelperCount: list queries count it with a subquery (offers are not loaded there).
    /// </summary>
    public static AnnouncementDto AsDto(this Announcement entity) => entity.AsSummary<AnnouncementDto>();

    /// <summary>
    /// Offers and job exits only as far as <paramref name="viewerId"/> may see them (the rules are in the aggregate).
    /// <paramref name="isLiked"/> – whether the viewer has liked it (read separately, likes are not part of the aggregate).
    /// </summary>
    public static AnnouncementDetailsDto AsDetailsDto(this Announcement entity, UserId? viewerId, bool isLiked = false)
        => entity.AsSummary<AnnouncementDetailsDto>() with
        {
            Offers = entity.GetOffersVisibleTo(viewerId).OrderBy(o => o.CreatedAt).Select(o => o.AsDto(entity)).ToList(),
            HelperCount = entity.Offers.Select(o => o.HelperId).Distinct().Count(),
            IsLiked = isLiked,
            Images = entity.GetImagesInOrder().Select(i => i.AsDto()).ToList(),
            Changes = entity.Changes.OrderBy(c => c.Revision).Select(c => c.AsDto()).ToList(),
            JobExits = entity.GetJobExitsVisibleTo(viewerId).Select(e => e.AsDto()).ToList()
        };

    /// <summary>The list card with the values the query computed in the database (thumbnail, helpers, distance).</summary>
    public static AnnouncementDto AsDto(this AnnouncementListRow row)
        => row.Announcement.AsDto() with
        {
            DistanceKm = row.DistanceMeters is { } meters ? Math.Round(meters / 1000, 2) : null,
            ThumbnailUrl = ThumbnailUrl(row.Announcement.Id, row.ThumbnailId),
            HelperCount = row.HelperCount
        };

    /// <summary>URL of the thumbnail when its id comes from a subquery (null = the announcement has no images).</summary>
    public static string? ThumbnailUrl(Id announcementId, Id? thumbnailId)
        => thumbnailId is null ? null : ImageDto.GetUrl(announcementId, thumbnailId);

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

    // What the list card and the details have in common – written once for both DTOs.
    // The thumbnail needs the images loaded (list queries compute it with a subquery instead).
    private static TDto AsSummary<TDto>(this Announcement entity) where TDto : AnnouncementDto, new()
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
            ThumbnailUrl = ThumbnailUrl(entity.Id, entity.GetThumbnail()?.Id)
        };
}
