using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Time;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

internal sealed class StartConversationHandler : ICommandHandler<StartConversation>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly IConversationRepository _conversations;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public StartConversationHandler(IAnnouncementRepository announcements, IConversationRepository conversations,
        IUserRepository users, IUnitOfWork unitOfWork, IClock clock)
    {
        _announcements = announcements;
        _conversations = conversations;
        _users = users;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task HandleAsync(StartConversation command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            await _users.GetActiveOrThrowAsync(command.HelperId, cancellationToken);
            var announcement = await _announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);

            await _conversations.GetOrStartAsync(announcement, new UserId(command.HelperId),
                new Id(command.ConversationId), _clock.UtcNow, cancellationToken);
        }, cancellationToken);
}

internal sealed class SendMessageHandler : ICommandHandler<SendMessage>
{
    private readonly IConversationRepository _conversations;
    private readonly IAnnouncementRepository _announcements;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SendMessageHandler(IConversationRepository conversations, IAnnouncementRepository announcements,
        IUserRepository users, IUnitOfWork unitOfWork, IClock clock)
    {
        _conversations = conversations;
        _announcements = announcements;
        _users = users;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task HandleAsync(SendMessage command, CancellationToken cancellationToken = default)
    {
        var text = new Description(command.Text);

        await _unitOfWork.ExecuteAsync(async () =>
        {
            await _users.GetActiveOrThrowAsync(command.SenderId, cancellationToken);
            var conversation = await _conversations.GetOrThrowAsync(command.ConversationId, cancellationToken);

            // Czy wolno jeszcze pisać, zależy od statusu ogłoszenia (osobny agregat – tylko odczyt).
            var announcement = await _announcements.GetOrThrowAsync(conversation.AnnouncementId, cancellationToken);

            conversation.SendMessage(new Id(command.MessageId), new UserId(command.SenderId), text, announcement,
                _clock.UtcNow);
        }, cancellationToken);
    }
}

internal sealed class MarkConversationAsReadHandler : ICommandHandler<MarkConversationAsRead>
{
    private readonly IConversationRepository _conversations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public MarkConversationAsReadHandler(IConversationRepository conversations, IUnitOfWork unitOfWork, IClock clock)
    {
        _conversations = conversations;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task HandleAsync(MarkConversationAsRead command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var conversation = await _conversations.GetOrThrowAsync(command.ConversationId, cancellationToken);
            conversation.MarkAsRead(new UserId(command.UserId), _clock.UtcNow);
        }, cancellationToken);
}
