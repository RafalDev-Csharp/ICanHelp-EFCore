using ICanHelp.Application.Commands;
using ICanHelp.Application.Commands.Handlers;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Tests.Fakes;
using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Tests.Commands;

/// <summary>Krok 11: handlery wątku negocjacji na fake'ach. Reguły domeny – w Core.Tests (ConversationTests).</summary>
public class ConversationHandlersTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryAnnouncementRepository _announcements = new();
    private readonly InMemoryConversationRepository _conversations = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FixedClock _clock = new();

    private readonly User _owner;
    private readonly User _helper;
    private readonly Announcement _announcement;

    public ConversationHandlersTests()
    {
        _owner = AddUser("jan.kowalski", "jan@example.com");
        _helper = AddUser("anna.nowak", "anna@example.com");

        _announcement = new Announcement(Id.New(), _owner.Id, Id.New(), Id.New(), DifficultyLevel.Easy,
            "Cieknący kran", "Kran w kuchni cieknie od tygodnia.", Price.Pln(150), new Location(50.06, 19.94),
            _clock.UtcNow.AddDays(-1));
        _announcements.Announcements.Add(_announcement);
    }

    // ---------- StartConversation ----------

    [Fact]
    public async Task StartConversation_ByHelper_AddsConversation()
    {
        var conversationId = Guid.NewGuid();

        await StartHandler().HandleAsync(new StartConversation(_announcement.Id, conversationId, _helper.Id));

        var conversation = _conversations.Conversations.ShouldHaveSingleItem();
        conversation.Id.Value.ShouldBe(conversationId);
        conversation.AnnouncementId.ShouldBe(_announcement.Id);
        conversation.ClientId.ShouldBe(_owner.Id);
        conversation.HelperId.ShouldBe(_helper.Id);
        conversation.CreatedAt.ShouldBe(_clock.UtcNow);
    }

    [Fact]
    public async Task StartConversation_Twice_KeepsTheFirstOne()
    {
        var firstId = Guid.NewGuid();
        await StartHandler().HandleAsync(new StartConversation(_announcement.Id, firstId, _helper.Id));

        await StartHandler().HandleAsync(new StartConversation(_announcement.Id, Guid.NewGuid(), _helper.Id));

        _conversations.Conversations.ShouldHaveSingleItem().Id.Value.ShouldBe(firstId);
    }

    [Fact]
    public async Task StartConversation_ByOwner_Throws()
    {
        await Should.ThrowAsync<DomainRuleException>(() =>
            StartHandler().HandleAsync(new StartConversation(_announcement.Id, Guid.NewGuid(), _owner.Id)));

        _conversations.Conversations.ShouldBeEmpty();
    }

    [Fact]
    public async Task StartConversation_UnknownAnnouncement_Throws()
    {
        await Should.ThrowAsync<AnnouncementNotFoundException>(() =>
            StartHandler().HandleAsync(new StartConversation(Guid.NewGuid(), Guid.NewGuid(), _helper.Id)));
    }

    [Fact]
    public async Task StartConversation_LockedHelper_Throws()
    {
        _helper.Lock();

        await Should.ThrowAsync<UserLockedException>(() =>
            StartHandler().HandleAsync(new StartConversation(_announcement.Id, Guid.NewGuid(), _helper.Id)));
    }

    // ---------- MakeOffer zakłada wątek ----------

    [Fact]
    public async Task MakeOffer_WithoutConversation_StartsIt()
    {
        await MakeOfferAsync();

        var conversation = _conversations.Conversations.ShouldHaveSingleItem();
        conversation.HelperId.ShouldBe(_helper.Id);
        conversation.AnnouncementId.ShouldBe(_announcement.Id);
    }

    [Fact]
    public async Task MakeOffer_AfterQuestion_UsesExistingConversation()
    {
        var conversationId = Guid.NewGuid();
        await StartHandler().HandleAsync(new StartConversation(_announcement.Id, conversationId, _helper.Id));

        await MakeOfferAsync();

        _conversations.Conversations.ShouldHaveSingleItem().Id.Value.ShouldBe(conversationId);
    }

    // ---------- SendMessage / MarkAsRead ----------

    [Fact]
    public async Task SendMessage_ByParticipant_AddsMessage()
    {
        var conversationId = await StartAsync();

        await SendHandler().HandleAsync(new SendMessage
        {
            ConversationId = conversationId,
            MessageId = Guid.NewGuid(),
            SenderId = _owner.Id,
            Text = "Uszczelka jest, wystarczy ją wymienić."
        });

        var message = _conversations.Conversations.Single().Messages.ShouldHaveSingleItem();
        message.SenderId.ShouldBe(_owner.Id);
        message.Text.Value.ShouldBe("Uszczelka jest, wystarczy ją wymienić.");
        message.SentAt.ShouldBe(_clock.UtcNow);
        message.AnnouncementRevision.ShouldBe(0);
    }

    [Fact]
    public async Task SendMessage_ByStranger_Throws()
    {
        var conversationId = await StartAsync();
        var stranger = AddUser("obcy", "obcy@example.com");

        await Should.ThrowAsync<DomainRuleException>(() => SendHandler().HandleAsync(new SendMessage
        {
            ConversationId = conversationId,
            MessageId = Guid.NewGuid(),
            SenderId = stranger.Id,
            Text = "Dzień dobry"
        }));
    }

    [Fact]
    public async Task SendMessage_EmptyText_ThrowsBeforeTransaction()
    {
        var conversationId = await StartAsync();
        var executions = _unitOfWork.Executions;

        await Should.ThrowAsync<InvalidDescriptionException>(() => SendHandler().HandleAsync(new SendMessage
        {
            ConversationId = conversationId,
            MessageId = Guid.NewGuid(),
            SenderId = _helper.Id,
            Text = "  "
        }));

        _unitOfWork.Executions.ShouldBe(executions);
    }

    [Fact]
    public async Task SendMessage_UnknownConversation_Throws()
    {
        await Should.ThrowAsync<ConversationNotFoundException>(() => SendHandler().HandleAsync(new SendMessage
        {
            ConversationId = Guid.NewGuid(),
            MessageId = Guid.NewGuid(),
            SenderId = _helper.Id,
            Text = "Dzień dobry"
        }));
    }

    [Fact]
    public async Task MarkAsRead_SetsReadTimeOfThatSide()
    {
        var conversationId = await StartAsync();

        await new MarkConversationAsReadHandler(_conversations, _unitOfWork, _clock)
            .HandleAsync(new MarkConversationAsRead(conversationId, _owner.Id));

        var conversation = _conversations.Conversations.Single();
        conversation.ClientLastReadAt.ShouldBe(_clock.UtcNow);
        conversation.HelperLastReadAt.ShouldBeNull();
    }

    // ---------- pomocnicze ----------

    private User AddUser(string userName, string email)
    {
        var user = new User(UserId.New(), userName, "Test User", FakePasswordManager.Prefix + "Haslo123!",
            new ContactData(email, "+48600100200", "Polska", "Kraków", "30-001"), _clock.UtcNow);
        _users.Users.Add(user);
        return user;
    }

    private StartConversationHandler StartHandler()
        => new(_announcements, _conversations, _users, _unitOfWork, _clock);

    private SendMessageHandler SendHandler()
        => new(_conversations, _announcements, _users, _unitOfWork, _clock);

    private async Task<Guid> StartAsync()
    {
        var conversationId = Guid.NewGuid();
        await StartHandler().HandleAsync(new StartConversation(_announcement.Id, conversationId, _helper.Id));
        return conversationId;
    }

    private Task MakeOfferAsync()
        => new MakeOfferHandler(_announcements, _users, _conversations, _unitOfWork, _clock).HandleAsync(new MakeOffer
        {
            AnnouncementId = _announcement.Id,
            OfferId = Guid.NewGuid(),
            HelperId = _helper.Id,
            Price = new PriceDto { Amount = 120, Currency = "PLN" },
            ProposedStart = _clock.UtcNow.AddDays(1),
            ProposedEnd = _clock.UtcNow.AddDays(1).AddHours(2)
        });
}
