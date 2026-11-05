using ICanHelp.Core.Exceptions;

namespace ICanHelp.Application.Exceptions;

public class AnnouncementNotFoundException : CustomExceptions
{
    public Guid AnnouncementId { get; }

    public AnnouncementNotFoundException(Guid announcementId)
        : base($"Announcement with id: {announcementId} was not found.")
    {
        AnnouncementId = announcementId;
    }
}
