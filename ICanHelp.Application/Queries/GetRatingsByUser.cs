using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Queries;

/// <summary>Ratings a user has received - the base of his reputation.</summary>
public sealed class GetRatingsByUser : IQuery<IReadOnlyList<RatingDto>>
{
    public Guid UserId { get; init; }
}
