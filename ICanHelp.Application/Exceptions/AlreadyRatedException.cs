using ICanHelp.Core.Exceptions;

namespace ICanHelp.Application.Exceptions;

public class AlreadyRatedException : CustomExceptions
{
    public Guid AnnouncementId { get; }

    public AlreadyRatedException(Guid announcementId)
        : base($"You have already rated announcement: {announcementId}.")
    {
        AnnouncementId = announcementId;
    }
}
