using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Time;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

internal sealed class MarkNotificationAsReadHandler : ICommandHandler<MarkNotificationAsRead>
{
    private readonly INotificationRepository _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public MarkNotificationAsReadHandler(INotificationRepository notifications, IUnitOfWork unitOfWork, IClock clock)
    {
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task HandleAsync(MarkNotificationAsRead command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var userId = new UserId(command.UserId);
            var notification = await _notifications.GetByIdAsync(new Id(command.NotificationId), cancellationToken);

            // Cudze powiadomienie wygląda jak nieistniejące (404), nie 409 – nie zdradzamy, że istnieje.
            if (notification is null || notification.UserId != userId)
            {
                throw new NotificationNotFoundException(command.NotificationId);
            }

            notification.MarkAsRead(userId, _clock.UtcNow);
        }, cancellationToken);
}

internal sealed class MarkAllNotificationsAsReadHandler : ICommandHandler<MarkAllNotificationsAsRead>
{
    private readonly INotificationRepository _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public MarkAllNotificationsAsReadHandler(INotificationRepository notifications, IUnitOfWork unitOfWork,
        IClock clock)
    {
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task HandleAsync(MarkAllNotificationsAsRead command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var userId = new UserId(command.UserId);
            var now = _clock.UtcNow;

            foreach (var notification in await _notifications.GetUnreadAsync(userId, cancellationToken))
            {
                notification.MarkAsRead(userId, now);
            }
        }, cancellationToken);
}
