using ICanHelp.Core.Exceptions;

namespace ICanHelp.Application.Exceptions;

public class NotificationNotFoundException : CustomExceptions
{
    public Guid NotificationId { get; }

    public NotificationNotFoundException(Guid notificationId)
        : base($"Notification with id: {notificationId} was not found.")
    {
        NotificationId = notificationId;
    }
}
