using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Time;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

internal sealed class StartConversationHandler(IAnnouncementRepository announcements,
    IConversationRepository conversations, IUserRepository users, IUnitOfWork unitOfWork, IClock clock)
    : ICommandHandler<StartConversation>
{
    public Task HandleAsync(StartConversation command, CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async () =>
        {
            await users.GetActiveOrThrowAsync(command.HelperId, cancellationToken);
            var announcement = await announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);

            await conversations.GetOrStartAsync(announcement, new UserId(command.HelperId),
                new Id(command.ConversationId), clock.UtcNow, cancellationToken);
        }, cancellationToken);
}

internal sealed class SendMessageHandler(IConversationRepository conversations, IAnnouncementRepository announcements,
    IUserRepository users, IUnitOfWork unitOfWork, IClock clock) : ICommandHandler<SendMessage>
{
    public async Task HandleAsync(SendMessage command, CancellationToken cancellationToken = default)
    {
        var text = new Description(command.Text);

        await unitOfWork.ExecuteAsync(async () =>
        {
            await users.GetActiveOrThrowAsync(command.SenderId, cancellationToken);
            var conversation = await conversations.GetOrThrowAsync(command.ConversationId, cancellationToken);

            // Czy wolno jeszcze pisać, zależy od statusu ogłoszenia (osobny agregat – tylko odczyt).
            var announcement = await announcements.GetOrThrowAsync(conversation.AnnouncementId, cancellationToken);

            conversation.SendMessage(new Id(command.MessageId), new UserId(command.SenderId), text, announcement,
                clock.UtcNow);
        }, cancellationToken);
    }
}

internal sealed class MarkConversationAsReadHandler(IConversationRepository conversations, IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<MarkConversationAsRead>
{
    public Task HandleAsync(MarkConversationAsRead command, CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async () =>
        {
            var conversation = await conversations.GetOrThrowAsync(command.ConversationId, cancellationToken);
            conversation.MarkAsRead(new UserId(command.UserId), clock.UtcNow);
        }, cancellationToken);
}
