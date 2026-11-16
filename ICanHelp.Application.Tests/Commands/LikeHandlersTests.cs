using ICanHelp.Application.Commands;
using ICanHelp.Application.Commands.Handlers;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Tests.Fakes;
using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Enums;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Tests.Commands;

/// <summary>Krok F4: polubienia ogłoszeń – idempotentne dodanie i usunięcie.</summary>
public class LikeHandlersTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryAnnouncementRepository _announcements = new();
    private readonly InMemoryLikedAnnouncementRepository _likes = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FixedClock _clock = new();

    private readonly User _owner;
    private readonly User _fan;
    private readonly Announcement _announcement;

    public LikeHandlersTests()
    {
        _owner = AddUser("wlasciciel", "wlasciciel@example.com");
        _fan = AddUser("fan", "fan@example.com");

        _announcement = new Announcement(Id.New(), _owner.Id, Id.New(), Id.New(), DifficultyLevel.Easy,
            "Cieknący kran", "Kran w kuchni cieknie od tygodnia.", Price.Pln(150), new Location(50.06, 19.94),
            _clock.UtcNow.AddDays(-1));
        _announcements.Announcements.Add(_announcement);
    }

    [Fact]
    public async Task Like_Twice_StoresOneLikeWithTime()
    {
        await LikeHandler().HandleAsync(new LikeAnnouncement(_announcement.Id, _fan.Id));
        await LikeHandler().HandleAsync(new LikeAnnouncement(_announcement.Id, _fan.Id));

        var like = _likes.Likes.ShouldHaveSingleItem();
        like.UserId.ShouldBe(_fan.Id);
        like.AnnouncementId.ShouldBe(_announcement.Id);
        like.LikedAt.ShouldBe(_clock.UtcNow);
    }

    [Fact]
    public async Task Like_UnknownAnnouncement_Throws()
    {
        await Should.ThrowAsync<AnnouncementNotFoundException>(() =>
            LikeHandler().HandleAsync(new LikeAnnouncement(Guid.NewGuid(), _fan.Id)));

        _likes.Likes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Like_LockedUser_Throws()
    {
        _fan.Lock();

        await Should.ThrowAsync<UserLockedException>(() =>
            LikeHandler().HandleAsync(new LikeAnnouncement(_announcement.Id, _fan.Id)));
    }

    [Fact]
    public async Task Unlike_RemovesLike_AndIsIdempotent()
    {
        await LikeHandler().HandleAsync(new LikeAnnouncement(_announcement.Id, _fan.Id));

        await UnlikeHandler().HandleAsync(new UnlikeAnnouncement(_announcement.Id, _fan.Id));
        await UnlikeHandler().HandleAsync(new UnlikeAnnouncement(_announcement.Id, _fan.Id));

        _likes.Likes.ShouldBeEmpty();
    }

    private LikeAnnouncementHandler LikeHandler() => new(_likes, _announcements, _users, _unitOfWork, _clock);

    private UnlikeAnnouncementHandler UnlikeHandler() => new(_likes, _unitOfWork);

    private User AddUser(string userName, string email)
    {
        var user = new User(UserId.New(), userName, "Test User", FakePasswordManager.Prefix + "Haslo123!",
            new ContactData(email, "+48600100200", "Polska", "Kraków", "30-001"), _clock.UtcNow);
        _users.Users.Add(user);
        return user;
    }
}
