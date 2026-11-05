using ICanHelp.Core.Exceptions;

namespace ICanHelp.Core.ValueObjects;

/// <summary>A point on the map (WGS84 coordinates).</summary>
public sealed record Location
{
    public double Latitude { get; }
    public double Longitude { get; }

    public Location(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || latitude is < -90 or > 90)
        {
            throw new DomainRuleException("Latitude must be between -90 and 90.");
        }

        if (double.IsNaN(longitude) || longitude is < -180 or > 180)
        {
            throw new DomainRuleException("Longitude must be between -180 and 180.");
        }

        Latitude = latitude;
        Longitude = longitude;
    }

    public override string ToString() => FormattableString.Invariant($"{Latitude}, {Longitude}");
}
