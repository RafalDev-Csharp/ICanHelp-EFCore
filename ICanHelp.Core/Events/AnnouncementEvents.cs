using ICanHelp.Core.Enums;

namespace ICanHelp.Core.Events;

/// <summary>A new announcement was published (Krok 14: alerts about new announcements nearby / on the way).</summary>
public sealed record AnnouncementPublished(
    Guid AnnouncementId,
    Guid OwnerId,
    Guid CategoryId,
    Guid SubcategoryId,
    double Latitude,
    double Longitude,
    DateTime OccurredAt) : IDomainEvent;

/// <summary>
/// The owner really changed the announcement (edit or image added/removed) – a new <paramref name="Revision"/>.
/// An edit without changes raises nothing.
/// </summary>
public sealed record AnnouncementEdited(
    Guid AnnouncementId,
    Guid OwnerId,
    int Revision,
    AnnouncementFields ChangedFields,
    DateTime OccurredAt) : IDomainEvent;
