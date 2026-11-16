using ICanHelp.Core.Enums;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities;

/// <summary>
/// One edit of a published announcement: which revision it created, when, and which parts changed.
/// Lets the other side see "the announcement changed after your offer: description, images".
/// Created only through <see cref="Announcement"/>.
/// </summary>
public class AnnouncementChange
{
    private readonly List<AnnouncementFieldChange> _fields = new();

    public Id AnnouncementId { get; private set; }

    /// <summary>The revision created by this edit (1 for the first edit, 2 for the next...).</summary>
    public int Revision { get; private set; }

    public DateTime ChangedAt { get; private set; }

    /// <summary>All changed parts together (quick filter / badge). Details with old and new values: <see cref="Fields"/>.</summary>
    public AnnouncementFields ChangedFields { get; private set; }

    public IReadOnlyCollection<AnnouncementFieldChange> Fields => _fields.AsReadOnly();

    // Required by EF Core.
    private AnnouncementChange()
    {
    }

    internal AnnouncementChange(Id announcementId, int revision, DateTime changedAtUtc,
        IEnumerable<AnnouncementFieldChange> fields)
    {
        AnnouncementId = announcementId;
        Revision = revision;
        ChangedAt = changedAtUtc;
        _fields.AddRange(fields);
        ChangedFields = _fields.Aggregate(AnnouncementFields.None, (all, field) => all | field.Field);
    }
}
