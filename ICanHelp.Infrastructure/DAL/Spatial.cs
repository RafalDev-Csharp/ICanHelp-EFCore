using ICanHelp.Core.ValueObjects;
using NetTopologySuite;
using NetTopologySuite.Geometries;
// NetTopologySuite.Geometries ma własny typ Location – tu chodzi o punkt z domeny.
using Location = ICanHelp.Core.ValueObjects.Location;

namespace ICanHelp.Infrastructure.DAL;

/// <summary>
/// Helpers for distance search and alerts (SQL Server <c>geography</c> + NetTopologySuite).
/// The domain keeps a plain <c>Location</c> (lat/lon); the <c>geography</c> columns exist only in the database
/// as shadow properties, so Core stays free of NetTopologySuite.
/// </summary>
internal static class Spatial
{
    /// <summary>WGS84 – the coordinate system of GPS; with it SQL Server returns distances in metres.</summary>
    public const int Wgs84Srid = 4326;

    /// <summary>Name of the shadow property / column on Announcements.</summary>
    public const string GeoPoint = nameof(GeoPoint);

    /// <summary>AnnouncementAlerts: point (area) or LINESTRING (route), written by EF from the alert's points.</summary>
    public const string AlertShape = "Shape";

    /// <summary>AnnouncementAlerts: Shape buffered by RadiusKm (metres), computed by the trigger, spatial index.</summary>
    public const string AlertZone = "Zone";

    /// <summary>Trigger that recomputes Zone when Shape or RadiusKm change (migration AnnouncementAlerts).</summary>
    public const string AlertZoneTrigger = "TR_AnnouncementAlerts_Zone";

    private static readonly GeometryFactory Factory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: Wgs84Srid);

    /// <summary>NTS uses X = longitude, Y = latitude (the opposite order to geography::Point in SQL).</summary>
    public static Point CreatePoint(double latitude, double longitude)
        => Factory.CreatePoint(new Coordinate(longitude, latitude));

    /// <summary>One point → Point (area alert), more → LineString (route). The route is simplified by SQL (Reduce).</summary>
    public static Geometry CreateShape(IReadOnlyList<Location> points)
        => points.Count == 1
            ? CreatePoint(points[0].Latitude, points[0].Longitude)
            : Factory.CreateLineString(points.Select(p => new Coordinate(p.Longitude, p.Latitude)).ToArray());
}
