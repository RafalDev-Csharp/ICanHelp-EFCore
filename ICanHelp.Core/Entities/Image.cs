using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities;

/// <summary>A photo attached to an announcement. Created only through <see cref="Announcement.AddImage"/>.</summary>
public class Image
{
    public Id Id { get; private set; }
    public Id AnnouncementId { get; private set; }

    /// <summary>Original file name (as sent by the user) – only for display.</summary>
    public StringObject FileName { get; private set; }

    /// <summary>Extension of the stored file, from the real content (".jpg", ".png", ".webp").</summary>
    public StringObject FileExtension { get; private set; }

    /// <summary>Where the file is kept in the file storage (relative path). Not shown to clients.</summary>
    public StringObject FilePath { get; private set; }

    /// <summary>When the image was added. The oldest image is the thumbnail of the announcement.</summary>
    public DateTime AddedAt { get; private set; }

    // Required by EF Core.
    private Image()
    {
    }

    internal Image(Id id, Id announcementId, StringObject fileName, StringObject fileExtension, StringObject filePath,
        DateTime addedAtUtc)
    {
        Id = id;
        AnnouncementId = announcementId;
        FileName = fileName;
        FileExtension = fileExtension;
        FilePath = filePath;
        AddedAt = addedAtUtc;
    }
}
