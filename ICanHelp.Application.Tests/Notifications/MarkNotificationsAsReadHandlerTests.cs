using ICanHelp.Application.Commands;
using ICanHelp.Application.Commands.Handlers;
using ICanHelp.Application.Tests.Fakes;
using ICanHelp.Core.Entities.Notifications;
using ICanHelp.Core.Enums;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Tests.Notifications;

/// <summary>„Oznacz wszystkie” – cała lista albo tylko jeden wątek (F6: otwarcie rozmowy gasi jej powiadomienia).</summary>
public class MarkNotificationsAsReadHandlerTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryNotificationRepository _notifications = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FixedClock _clock = new() { UtcNow = Now.AddHours(1) };
    private readonly MarkAllNotificationsAsReadHandler _handler;

    private readonly UserId _user = UserId.New();
    private readonly Id _conversation = Id.New();
    private readonly Id _otherConversation = Id.New();

    public MarkNotificationsAsReadHandlerTests()
    {
        _handler = new MarkAllNotificationsAsReadHandler(_notifications, _unitOfWork, _clock);
    }

    [Fact]
    public async Task WithoutConversation_MarksAllOfTheUser_NotOthers()
    {
        var mine = Add(_user, _conversation);
        var mineOther = Add(_user, null);
        var somebodyElses = Add(UserId.New(), _conversation);

        await _handler.HandleAsync(new MarkAllNotificationsAsRead(_user.Value));

        mine.ReadAt.ShouldBe(Now.AddHours(1));
        mineOther.ReadAt.ShouldBe(Now.AddHours(1));
        somebodyElses.ReadAt.ShouldBeNull();
        _unitOfWork.Executions.ShouldBe(1);
    }

    [Fact]
    public async Task WithConversation_MarksOnlyThatThread()
    {
        var inThread = Add(_user, _conversation);
        var inOtherThread = Add(_user, _otherConversation);
        var withoutThread = Add(_user, null);

        await _handler.HandleAsync(new MarkAllNotificationsAsRead(_user.Value, _conversation.Value));

        inThread.ReadAt.ShouldBe(Now.AddHours(1));
        inOtherThread.ReadAt.ShouldBeNull();
        withoutThread.ReadAt.ShouldBeNull();
    }

    private Notification Add(UserId userId, Id? conversationId)
    {
        var notification = new Notification(Id.New(), userId, NotificationType.MessageReceived, "{}", Id.New(),
            conversationId, Guid.NewGuid(), Now);
        _notifications.Notifications.Add(notification);
        return notification;
    }
}
