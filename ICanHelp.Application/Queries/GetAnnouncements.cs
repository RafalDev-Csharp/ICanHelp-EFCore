using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Exceptions;
using ICanHelp.Core.Enums;

namespace ICanHelp.Application.Queries;

/// <summary>
/// A page of announcements. Every filter is optional.
/// Without a location: newest first. With <see cref="Latitude"/> + <see cref="Longitude"/>: only announcements
/// within <see cref="RadiusKm"/> of that point, nearest first (and <see cref="AnnouncementDto.DistanceKm"/> is filled).
/// </summary>
public sealed class GetAnnouncements : IQuery<IReadOnlyList<AnnouncementDto>>
{
    public const double DefaultRadiusKm = 10;
    public const double MaxRadiusKm = 100;

    public Guid? OwnerId { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? SubcategoryId { get; init; }
    public AnnouncementStatus? Status { get; init; }

    /// <summary>Search centre – latitude (WGS84, -90..90). Must be given together with <see cref="Longitude"/>.</summary>
    public double? Latitude { get; init; }

    /// <summary>Search centre – longitude (WGS84, -180..180). Must be given together with <see cref="Latitude"/>.</summary>
    public double? Longitude { get; init; }

    /// <summary>Search radius in kilometres (default 10, max 100). Only together with a location.</summary>
    public double? RadiusKm { get; init; }

    /// <summary>
    /// Text to look for in the title or description (case-insensitive, max <see cref="MaxPhraseLength"/> characters).
    /// Empty / whitespace = no filter.
    /// </summary>
    public string? Phrase { get; init; }

    public const int MaxPhraseLength = 100;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// The trimmed search phrase as a SQL <c>LIKE</c> pattern (<c>%phrase%</c>, wildcards in the phrase escaped with
    /// <c>\</c>), or <c>null</c> when no phrase was given. Throws <see cref="InvalidSearchPhraseException"/> (→ 400)
    /// when the phrase is too long.
    /// </summary>
    public string? GetPhrasePattern()
    {
        var phrase = Phrase?.Trim();
        if (string.IsNullOrEmpty(phrase))
        {
            return null;
        }

        if (phrase.Length > MaxPhraseLength)
        {
            throw new InvalidSearchPhraseException(
                FormattableString.Invariant($"Phrase must be at most {MaxPhraseLength} characters."));
        }

        var escaped = phrase
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", @"\_")
            .Replace("[", @"\[");
        return $"%{escaped}%";
    }

    /// <summary>
    /// The distance filter, or <c>null</c> when no location was given.
    /// Throws <see cref="InvalidDistanceFilterException"/> (→ 400) when the parameters are incomplete or out of range.
    /// </summary>
    public DistanceFilter? GetDistanceFilter()
    {
        if (Latitude is null && Longitude is null)
        {
            if (RadiusKm is not null)
            {
                throw new InvalidDistanceFilterException("RadiusKm requires Latitude and Longitude.");
            }

            return null;
        }

        if (Latitude is not { } latitude || Longitude is not { } longitude)
        {
            throw new InvalidDistanceFilterException("Latitude and Longitude must be given together.");
        }

        if (double.IsNaN(latitude) || latitude is < -90 or > 90)
        {
            throw new InvalidDistanceFilterException("Latitude must be between -90 and 90.");
        }

        if (double.IsNaN(longitude) || longitude is < -180 or > 180)
        {
            throw new InvalidDistanceFilterException("Longitude must be between -180 and 180.");
        }

        var radiusKm = RadiusKm ?? DefaultRadiusKm;
        if (double.IsNaN(radiusKm) || radiusKm is <= 0 or > MaxRadiusKm)
        {
            throw new InvalidDistanceFilterException(
                FormattableString.Invariant($"RadiusKm must be greater than 0 and at most {MaxRadiusKm}."));
        }

        return new DistanceFilter(latitude, longitude, radiusKm);
    }
}

/// <summary>A validated "near this point" filter.</summary>
public sealed record DistanceFilter(double Latitude, double Longitude, double RadiusKm)
{
    public double RadiusMeters => RadiusKm * 1000;
}
