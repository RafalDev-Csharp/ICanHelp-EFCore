using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities;

/// <summary>A photo attached to an announcement. Created only through <see cref="Announcement.AddImage"/>.</summary>
public class Image
{
    public Id Id { get; private set; }
    public Id AnnouncementId { get; private set; }

    public StringObject FileName { get; private set; }
    public StringObject FileExtension { get; private set; }
    public StringObject FilePath { get; private set; }

    // Required by EF Core.
    private Image()
    {
    }

    internal Image(Id id, Id announcementId, StringObject fileName, StringObject fileExtension, StringObject filePath)
    {
        Id = id;
        AnnouncementId = announcementId;
        FileName = fileName;
        FileExtension = fileExtension;
        FilePath = filePath;
    }
}
