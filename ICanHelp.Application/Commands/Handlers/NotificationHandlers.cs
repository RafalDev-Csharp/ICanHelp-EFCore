using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Time;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

internal sealed class MarkNotificationAsReadHandler(INotificationRepository notifications, IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<MarkNotificationAsRead>
{
    public Task HandleAsync(MarkNotificationAsRead command, CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async () =>
        {
            var userId = new UserId(command.UserId);
            var notification = await notifications.GetOwnedOrThrowAsync(command.NotificationId, userId,
                cancellationToken);

            notification.MarkAsRead(userId, clock.UtcNow);
        }, cancellationToken);
}

internal sealed class MarkAllNotificationsAsReadHandler(INotificationRepository notifications, IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<MarkAllNotificationsAsRead>
{
    public Task HandleAsync(MarkAllNotificationsAsRead command, CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async () =>
        {
            var userId = new UserId(command.UserId);
            var now = clock.UtcNow;

            var unread = await notifications.GetUnreadAsync(userId, cancellationToken);
            Id? conversationId = command.ConversationId is { } id ? new Id(id) : null;

            foreach (var notification in unread.Where(n => conversationId is null || n.ConversationId == conversationId))
            {
                notification.MarkAsRead(userId, now);
            }
        }, cancellationToken);
}
