using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Queries;

/// <summary>Ratings given after an announcement was completed (at most one from each side).</summary>
public sealed class GetRatingByAnnouncementId : IQuery<IReadOnlyList<RatingDto>>
{
    public Guid AnnouncementId { get; init; }
}
