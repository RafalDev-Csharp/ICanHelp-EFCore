using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities.Alerts;

/// <summary>
/// Aggregate root (Krok 14): "let me know about new announcements of this category near here / on my way".
/// <list type="bullet">
/// <item><see cref="AlertKind.Area"/> – <see cref="RadiusKm"/> around one point (<see cref="Center"/>, e.g. home),</item>
/// <item><see cref="AlertKind.Route"/> – <see cref="RadiusKm"/> on both sides of a line (<see cref="RoutePoints"/>, e.g.
/// work → home; the polyline comes from a routing service on the client).</item>
/// </list>
/// The domain keeps plain points; the geometry (<c>Shape</c> = point or LINESTRING, <c>Zone</c> = shape buffered by the
/// radius in metres) exists only in the database (Infrastructure), so Core stays free of NetTopologySuite.
/// Category, subcategories and the schedule are checked here (<see cref="Matches"/>, <see cref="IsActiveAt"/>).
/// </summary>
public class AnnouncementAlert
{
    public const int MaxPerUser = 10;
    public const double MinRadiusKm = 0.5;
    public const double MaxRadiusKm = 50;
    public const int MaxRoutePoints = 1000;
    public const int MaxSubcategories = 20;

    public Id Id { get; private set; }

    /// <summary>The owner – the only person who sees and changes the alert and gets its notifications.</summary>
    public UserId UserId { get; private set; }

    public StringObject Name { get; private set; }
    public AlertKind Kind { get; private set; }

    public Id CategoryId { get; private set; }

    /// <summary>Subcategories of <see cref="CategoryId"/>; empty = the whole category.</summary>
    public IReadOnlyList<Id> SubcategoryIds { get; private set; }

    /// <summary>Area: exactly one point (the centre). Route: the polyline, at least two points.</summary>
    public IReadOnlyList<Location> Points { get; private set; }

    public double RadiusKm { get; private set; }

    /// <summary>Days when the alert works (<see cref="WeekDays.All"/> = every day).</summary>
    public WeekDays ActiveDays { get; private set; }

    /// <summary>Optional hours (local time) when the alert works; both or none. From &gt; To = over midnight.</summary>
    public TimeOnly? ActiveFrom { get; private set; }
    public TimeOnly? ActiveTo { get; private set; }

    public bool IsEnabled { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public Location? Center => Kind == AlertKind.Area ? Points[0] : null;
    public IReadOnlyList<Location> RoutePoints => Kind == AlertKind.Route ? Points : Array.Empty<Location>();

    // Required by EF Core.
    private AnnouncementAlert()
    {
    }

    public AnnouncementAlert(Id id, UserId userId, StringObject name, AlertKind kind, Id categoryId,
        IEnumerable<Id> subcategoryIds, IEnumerable<Location> points, double radiusKm, DateTime createdAtUtc)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new InvalidAlertException($"Alert kind: {kind} is invalid.");
        }

        Id = id;
        UserId = userId;
        Kind = kind;
        Name = name;
        CategoryId = categoryId;
        SubcategoryIds = ToSubcategories(subcategoryIds);
        Points = ToPoints(kind, points);
        RadiusKm = ToRadius(radiusKm);
        ActiveDays = WeekDays.All;
        IsEnabled = true;
        CreatedAt = createdAtUtc;
    }

    /// <summary>A user can have at most <see cref="MaxPerUser"/> alerts (the handler counts them).</summary>
    public static void EnsureCanCreate(int alertsOwned)
    {
        if (alertsOwned >= MaxPerUser)
        {
            throw new DomainRuleException($"You can have at most {MaxPerUser} alerts.");
        }
    }

    /// <summary>
    /// New values (the handler fills the ones not sent in PATCH with the current ones). The kind cannot change –
    /// a route stays a route. The database recomputes the zone when the points or the radius change.
    /// </summary>
    public void Update(UserId actorId, StringObject name, Id categoryId, IEnumerable<Id> subcategoryIds,
        IEnumerable<Location> points, double radiusKm, DateTime changedAtUtc)
    {
        EnsureOwner(actorId);

        var newPoints = ToPoints(Kind, points);
        var newSubcategories = ToSubcategories(subcategoryIds);
        var newRadius = ToRadius(radiusKm);

        Name = name;
        CategoryId = categoryId;
        SubcategoryIds = newSubcategories;

        // A new list only when the points really differ – EF then does not rewrite the shape for nothing.
        if (!Points.SequenceEqual(newPoints))
        {
            Points = newPoints;
        }

        RadiusKm = newRadius;
        UpdatedAt = changedAtUtc;
    }

    /// <summary>When the alert works: days and optional hours (local time). No hours = all day.</summary>
    public void SetSchedule(UserId actorId, WeekDays activeDays, TimeOnly? activeFrom, TimeOnly? activeTo,
        DateTime changedAtUtc)
    {
        EnsureOwner(actorId);

        if (activeDays == WeekDays.None || (activeDays & ~WeekDays.All) != 0)
        {
            throw new InvalidAlertException("Choose at least one day of the week.");
        }

        if (activeFrom.HasValue != activeTo.HasValue)
        {
            throw new InvalidAlertException("Give both hours (from and to) or none.");
        }

        if (activeFrom.HasValue && activeFrom == activeTo)
        {
            throw new InvalidAlertException("Hours from and to must differ (no hours = all day).");
        }

        ActiveDays = activeDays;
        ActiveFrom = activeFrom;
        ActiveTo = activeTo;
        UpdatedAt = changedAtUtc;
    }

    public void Enable(UserId actorId, DateTime changedAtUtc) => SetEnabled(actorId, true, changedAtUtc);

    public void Disable(UserId actorId, DateTime changedAtUtc) => SetEnabled(actorId, false, changedAtUtc);

    public bool IsOwnedBy(UserId userId) => UserId == userId;

    /// <summary>Is the alert interested in an announcement of this category/subcategory (geometry is checked by the DB)?</summary>
    public bool Matches(Id categoryId, Id subcategoryId)
        => IsEnabled
           && CategoryId == categoryId
           && (SubcategoryIds.Count == 0 || SubcategoryIds.Contains(subcategoryId));

    /// <summary>
    /// Does the schedule allow a notification at <paramref name="localTime"/> (the user's time zone – the caller converts)?
    /// For hours over midnight (22:00–06:00) the day is the day of <paramref name="localTime"/>.
    /// </summary>
    public bool IsActiveAt(DateTime localTime)
    {
        if (!ActiveDays.Includes(localTime.DayOfWeek))
        {
            return false;
        }

        if (ActiveFrom is not { } from || ActiveTo is not { } to)
        {
            return true;
        }

        var time = TimeOnly.FromDateTime(localTime);
        return from < to
            ? time >= from && time < to
            : time >= from || time < to;
    }

    // ---------- rules ----------

    private void SetEnabled(UserId actorId, bool enabled, DateTime changedAtUtc)
    {
        EnsureOwner(actorId);

        if (IsEnabled == enabled)
        {
            return;
        }

        IsEnabled = enabled;
        UpdatedAt = changedAtUtc;
    }

    private void EnsureOwner(UserId actorId)
    {
        if (actorId != UserId)
        {
            throw new DomainRuleException("Only the owner can change the alert.");
        }
    }

    private static double ToRadius(double radiusKm)
    {
        if (double.IsNaN(radiusKm) || radiusKm is < MinRadiusKm or > MaxRadiusKm)
        {
            throw new InvalidAlertException($"Radius must be between {MinRadiusKm} and {MaxRadiusKm} km.");
        }

        return radiusKm;
    }

    private static Location[] ToPoints(AlertKind kind, IEnumerable<Location> points)
    {
        var list = (points ?? throw new InvalidAlertException("Points are required.")).ToArray();

        if (kind == AlertKind.Area)
        {
            if (list.Length != 1)
            {
                throw new InvalidAlertException("An area alert needs exactly one point (the centre).");
            }

            return list;
        }

        if (list.Length is < 2 or > MaxRoutePoints)
        {
            throw new InvalidAlertException($"A route needs 2–{MaxRoutePoints} points.");
        }

        // The database (geography LINESTRING) does not accept two identical points one after another.
        for (var i = 1; i < list.Length; i++)
        {
            if (list[i] == list[i - 1])
            {
                throw new InvalidAlertException("Two consecutive points of the route are the same.");
            }
        }

        return list;
    }

    private static Id[] ToSubcategories(IEnumerable<Id>? subcategoryIds)
    {
        var list = (subcategoryIds ?? []).Distinct().ToArray();

        if (list.Length > MaxSubcategories)
        {
            throw new InvalidAlertException($"An alert can have at most {MaxSubcategories} subcategories.");
        }

        return list;
    }
}
