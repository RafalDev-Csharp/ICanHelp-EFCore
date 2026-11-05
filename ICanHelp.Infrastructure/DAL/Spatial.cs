using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace ICanHelp.Infrastructure.DAL;

/// <summary>
/// Helpers for distance search (SQL Server <c>geography</c> + NetTopologySuite).
/// The domain keeps a plain <c>Location</c> (lat/lon); the <c>geography</c> column exists only in the database
/// as a shadow property computed from Latitude/Longitude, so Core stays free of NetTopologySuite.
/// </summary>
internal static class Spatial
{
    /// <summary>WGS84 – the coordinate system of GPS; with it SQL Server returns distances in metres.</summary>
    public const int Wgs84Srid = 4326;

    /// <summary>Name of the shadow property / column on Announcements.</summary>
    public const string GeoPoint = nameof(GeoPoint);

    private static readonly GeometryFactory Factory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: Wgs84Srid);

    /// <summary>NTS uses X = longitude, Y = latitude (the opposite order to geography::Point in SQL).</summary>
    public static Point CreatePoint(double latitude, double longitude)
        => Factory.CreatePoint(new Coordinate(longitude, latitude));
}
