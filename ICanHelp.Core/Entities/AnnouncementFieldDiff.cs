using ICanHelp.Core.Enums;

namespace ICanHelp.Core.Entities;

/// <summary>
/// Net difference of one field between two revisions: the value then → the value now.
/// Calculated (not stored) from <see cref="Announcement.Changes"/>.
/// </summary>
public sealed record AnnouncementFieldDiff(AnnouncementFields Field, string? OldValue, string? NewValue);
