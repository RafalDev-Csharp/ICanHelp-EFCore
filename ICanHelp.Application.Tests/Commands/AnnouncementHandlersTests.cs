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

/// <summary>
/// Handlery Kroku 6 na fake'ach: dodanie ogłoszenia, oferty, negocjacja, realizacja, ocena.
/// Reguły domeny mają własne testy w Core.Tests – tu sprawdzamy, że handler je wywołuje i zapisuje wynik.
/// </summary>
public class AnnouncementHandlersTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryAnnouncementRepository _announcements = new();
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly InMemoryRatingRepository _ratings = new();
    private readonly InMemoryConversationRepository _conversations = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FixedClock _clock = new();

    private readonly User _owner;
    private readonly User _helper;
    private readonly Category _category;
    private readonly Guid _subcategoryId = Guid.NewGuid();

    public AnnouncementHandlersTests()
    {
        _owner = AddUser("jan.kowalski", "jan@example.com");
        _helper = AddUser("anna.nowak", "anna@example.com");

        _category = new Category(Id.New(), "Hydraulika");
        _category.AddSubcategory(new Id(_subcategoryId), "Krany");
        _categories.Categories.Add(_category);
    }

    // ---------- CreateAnnouncement ----------

    [Fact]
    public async Task CreateAnnouncement_ValidData_AddsOpenAnnouncementOwnedByActor()
    {
        var command = CreateCommand();

        await CreateHandler().HandleAsync(command);

        var announcement = _announcements.Announcements.ShouldHaveSingleItem();
        announcement.Id.Value.ShouldBe(command.AnnouncementId);
        announcement.OwnerId.ShouldBe(_owner.Id);
        announcement.Status.ShouldBe(AnnouncementStatus.Open);
        announcement.Budget.ShouldBe(Price.Pln(150));
        announcement.Location.ShouldBe(new Location(50.06, 19.94));
        announcement.CreatedAt.ShouldBe(_clock.UtcNow);
        _unitOfWork.Executions.ShouldBe(1);
    }

    [Fact]
    public async Task CreateAnnouncement_SubcategoryFromOtherCategory_Throws()
    {
        var command = CreateCommand() with { SubcategoryId = Guid.NewGuid() };

        await Should.ThrowAsync<InvalidCategoryException>(() => CreateHandler().HandleAsync(command));
        _announcements.Announcements.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAnnouncement_UnknownCategory_Throws()
    {
        var command = CreateCommand() with { CategoryId = Guid.NewGuid() };

        await Should.ThrowAsync<InvalidCategoryException>(() => CreateHandler().HandleAsync(command));
    }

    [Fact]
    public async Task CreateAnnouncement_LockedUser_Throws()
    {
        _owner.Lock();

        await Should.ThrowAsync<UserLockedException>(() => CreateHandler().HandleAsync(CreateCommand()));
        _announcements.Announcements.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAnnouncement_UnknownUser_Throws()
    {
        var command = CreateCommand() with { OwnerId = Guid.NewGuid() };

        await Should.ThrowAsync<UserNotFoundException>(() => CreateHandler().HandleAsync(command));
    }

    // ---------- oferty ----------

    [Fact]
    public async Task MakeOffer_ByHelper_AddsPendingOffer()
    {
        var announcement = await CreateAnnouncementAsync();
        var command = OfferCommand(announcement.Id);

        await new MakeOfferHandler(_announcements, _users, _conversations, _unitOfWork, _clock).HandleAsync(command);

        var offer = announcement.Offers.ShouldHaveSingleItem();
        offer.Id.Value.ShouldBe(command.OfferId);
        offer.HelperId.ShouldBe(_helper.Id);
        offer.ProposedBy.ShouldBe(OfferParty.Helper);
        offer.Status.ShouldBe(OfferStatus.Pending);
        offer.Message!.Value.ShouldBe("Mogę wpaść po pracy.");
        offer.CreatedAt.ShouldBe(_clock.UtcNow);
    }

    [Fact]
    public async Task MakeOffer_DateWithoutTimeZone_IsTreatedAsUtc()
    {
        var announcement = await CreateAnnouncementAsync();
        var start = DateTime.SpecifyKind(_clock.UtcNow.AddDays(1), DateTimeKind.Unspecified);
        var command = OfferCommand(announcement.Id) with { ProposedStart = start, ProposedEnd = start.AddHours(2) };

        await new MakeOfferHandler(_announcements, _users, _conversations, _unitOfWork, _clock).HandleAsync(command);

        var offer = announcement.Offers.ShouldHaveSingleItem();
        offer.ProposedStart.Kind.ShouldBe(DateTimeKind.Utc);
        offer.ProposedStart.ShouldBe(_clock.UtcNow.AddDays(1));
    }

    [Fact]
    public async Task MakeOffer_ByOwner_Throws()
    {
        var announcement = await CreateAnnouncementAsync();
        var command = OfferCommand(announcement.Id) with { HelperId = _owner.Id };

        await Should.ThrowAsync<DomainRuleException>(
            () => new MakeOfferHandler(_announcements, _users, _conversations, _unitOfWork, _clock).HandleAsync(command));
    }

    [Fact]
    public async Task MakeOffer_UnknownAnnouncement_Throws()
    {
        var command = OfferCommand(Id.New());

        await Should.ThrowAsync<AnnouncementNotFoundException>(
            () => new MakeOfferHandler(_announcements, _users, _conversations, _unitOfWork, _clock).HandleAsync(command));
    }

    [Fact]
    public async Task CounterOffer_ByOwner_ReplacesOriginalWithClientOffer()
    {
        var announcement = await CreateAnnouncementAsync();
        var offerId = await MakeOfferAsync(announcement);
        var command = new CounterOffer
        {
            AnnouncementId = announcement.Id,
            OfferId = offerId,
            NewOfferId = Guid.NewGuid(),
            ActorId = _owner.Id,
            Price = new PriceDto { Amount = 100, Currency = "PLN" },
            ProposedStart = _clock.UtcNow.AddDays(2),
            ProposedEnd = _clock.UtcNow.AddDays(2).AddHours(2),
            Message = " "
        };

        await new CounterOfferHandler(_announcements, _unitOfWork, _clock).HandleAsync(command);

        announcement.Offers.Single(o => o.Id == new Id(offerId)).Status.ShouldBe(OfferStatus.Countered);
        var counter = announcement.Offers.Single(o => o.Id == new Id(command.NewOfferId));
        counter.ProposedBy.ShouldBe(OfferParty.Client);
        counter.HelperId.ShouldBe(_helper.Id);
        counter.ParentOfferId!.Value.ShouldBe(offerId);
        counter.Message.ShouldBeNull();
    }

    [Fact]
    public async Task ReviseOffer_OutdatedOfferByHelper_ReplacesItWithOfferOnCurrentRevision()
    {
        var announcement = await CreateAnnouncementAsync();
        var offerId = await MakeOfferAsync(announcement);
        announcement.Update(_owner.Id, announcement.CategoryId, announcement.SubcategoryId, announcement.Difficulty,
            announcement.Title, "Kran i syfon w kuchni.", announcement.Budget, announcement.Location, _clock.UtcNow);
        var command = new ReviseOffer
        {
            AnnouncementId = announcement.Id,
            OfferId = offerId,
            NewOfferId = Guid.NewGuid(),
            ActorId = _helper.Id,
            Price = new PriceDto { Amount = 180, Currency = "PLN" },
            ProposedStart = _clock.UtcNow.AddDays(2),
            ProposedEnd = _clock.UtcNow.AddDays(2).AddHours(2),
            Message = "Z syfonem drożej."
        };

        await new ReviseOfferHandler(_announcements, _unitOfWork, _clock).HandleAsync(command);

        announcement.Offers.Single(o => o.Id == new Id(offerId)).Status.ShouldBe(OfferStatus.Revised);
        var revised = announcement.Offers.Single(o => o.Id == new Id(command.NewOfferId));
        revised.Status.ShouldBe(OfferStatus.Pending);
        revised.ProposedBy.ShouldBe(OfferParty.Helper);
        revised.AnnouncementRevision.ShouldBe(announcement.Revision);
        revised.Price.ShouldBe(Price.Pln(180));
        revised.ParentOfferId!.Value.ShouldBe(offerId);
    }

    [Fact]
    public async Task ReviseOffer_UpToDateOffer_ReplacesIt()
    {
        // F11 D: autor zmienia oczekującą ofertę także wtedy, gdy jest aktualna.
        var announcement = await CreateAnnouncementAsync();
        var offerId = await MakeOfferAsync(announcement);
        var newOfferId = Guid.NewGuid();

        await new ReviseOfferHandler(_announcements, _unitOfWork, _clock)
            .HandleAsync(new ReviseOffer
            {
                AnnouncementId = announcement.Id,
                OfferId = offerId,
                NewOfferId = newOfferId,
                ActorId = _helper.Id,
                Price = new PriceDto { Amount = 180, Currency = "PLN" },
                ProposedStart = _clock.UtcNow.AddDays(2),
                ProposedEnd = _clock.UtcNow.AddDays(2).AddHours(2)
            });

        announcement.Offers.Single(o => o.Id == new Id(offerId)).Status.ShouldBe(OfferStatus.Revised);
        announcement.Offers.Single(o => o.Id == new Id(newOfferId)).Status.ShouldBe(OfferStatus.Pending);
    }

    [Fact]
    public async Task AcceptOffer_ByOwner_AcceptsAnnouncement()
    {
        var announcement = await CreateAnnouncementAsync();
        var offerId = await MakeOfferAsync(announcement);

        await new AcceptOfferHandler(_announcements, _unitOfWork, _clock)
            .HandleAsync(new AcceptOffer(announcement.Id, offerId, _owner.Id));

        announcement.Status.ShouldBe(AnnouncementStatus.Accepted);
        announcement.AcceptedOfferId!.Value.ShouldBe(offerId);
    }

    [Fact]
    public async Task AcceptOffer_ByAuthorOfTheOffer_Throws()
    {
        var announcement = await CreateAnnouncementAsync();
        var offerId = await MakeOfferAsync(announcement);

        await Should.ThrowAsync<DomainRuleException>(() => new AcceptOfferHandler(_announcements, _unitOfWork, _clock)
            .HandleAsync(new AcceptOffer(announcement.Id, offerId, _helper.Id)));
        announcement.Status.ShouldBe(AnnouncementStatus.Open);
    }

    [Fact]
    public async Task AcceptOffer_UnknownOffer_Throws()
    {
        var announcement = await CreateAnnouncementAsync();

        await Should.ThrowAsync<OfferNotFoundException>(() => new AcceptOfferHandler(_announcements, _unitOfWork, _clock)
            .HandleAsync(new AcceptOffer(announcement.Id, Guid.NewGuid(), _owner.Id)));
    }

    [Fact]
    public async Task RejectAndWithdraw_ChangeOfferStatus()
    {
        var announcement = await CreateAnnouncementAsync();
        var rejectedId = await MakeOfferAsync(announcement);

        await new RejectOfferHandler(_announcements, _unitOfWork, _clock)
            .HandleAsync(new RejectOffer(announcement.Id, rejectedId, _owner.Id));
        var withdrawnId = await MakeOfferAsync(announcement);
        await new WithdrawOfferHandler(_announcements, _unitOfWork, _clock)
            .HandleAsync(new WithdrawOffer(announcement.Id, withdrawnId, _helper.Id));

        announcement.Offers.Single(o => o.Id == new Id(rejectedId)).Status.ShouldBe(OfferStatus.Rejected);
        announcement.Offers.Single(o => o.Id == new Id(withdrawnId)).Status.ShouldBe(OfferStatus.Withdrawn);
        announcement.Status.ShouldBe(AnnouncementStatus.Open);
    }

    // ---------- realizacja ----------

    [Fact]
    public async Task StartAndComplete_FullFlow_CompletesAnnouncement()
    {
        var announcement = await AcceptedAnnouncementAsync();

        await new StartAnnouncementHandler(_announcements, _unitOfWork, _clock)
            .HandleAsync(new StartAnnouncement(announcement.Id, _helper.Id));
        announcement.Status.ShouldBe(AnnouncementStatus.InProgress);

        await new ReportAnnouncementDoneHandler(_announcements, _unitOfWork, _clock)
            .HandleAsync(new ReportAnnouncementDone(announcement.Id, _helper.Id));
        announcement.Status.ShouldBe(AnnouncementStatus.InProgress);
        announcement.DoneReportedAt.ShouldBe(_clock.UtcNow);

        await new CompleteAnnouncementHandler(_announcements, _unitOfWork, _clock)
            .HandleAsync(new CompleteAnnouncement(announcement.Id, _owner.Id));
        announcement.Status.ShouldBe(AnnouncementStatus.Completed);
    }

    [Fact]
    public async Task Start_ByOwner_Throws()
    {
        var announcement = await AcceptedAnnouncementAsync();

        await Should.ThrowAsync<DomainRuleException>(() => new StartAnnouncementHandler(_announcements, _unitOfWork, _clock)
            .HandleAsync(new StartAnnouncement(announcement.Id, _owner.Id)));
        announcement.Status.ShouldBe(AnnouncementStatus.Accepted);
    }

    [Fact]
    public async Task Complete_WhenAccepted_CompletesWithoutStart()
    {
        var announcement = await AcceptedAnnouncementAsync();

        await new CompleteAnnouncementHandler(_announcements, _unitOfWork, _clock)
            .HandleAsync(new CompleteAnnouncement(announcement.Id, _owner.Id));

        announcement.Status.ShouldBe(AnnouncementStatus.Completed);
    }

    [Fact]
    public async Task Cancel_ByHelper_Throws()
    {
        var announcement = await AcceptedAnnouncementAsync();

        await Should.ThrowAsync<DomainRuleException>(() => new CancelAnnouncementHandler(_announcements, _unitOfWork)
            .HandleAsync(new CancelAnnouncement(announcement.Id, _helper.Id)));
        announcement.Status.ShouldBe(AnnouncementStatus.Accepted);
    }

    [Fact]
    public async Task Cancel_ByOwner_CancelsAnnouncement()
    {
        var announcement = await CreateAnnouncementAsync();

        await new CancelAnnouncementHandler(_announcements, _unitOfWork)
            .HandleAsync(new CancelAnnouncement(announcement.Id, _owner.Id));

        announcement.Status.ShouldBe(AnnouncementStatus.Cancelled);
    }

    // ---------- F11 E: rezygnacja i przerwanie ----------

    [Fact]
    public async Task AbandonJob_ByHelper_ReopensAnnouncement()
    {
        var announcement = await AcceptedAnnouncementAsync();

        await new AbandonJobHandler(_announcements, _unitOfWork, _clock).HandleAsync(new AbandonJob
        {
            AnnouncementId = announcement.Id, ActorId = _helper.Id, Reason = JobExitReason.CannotMakeIt, Note = "  "
        });

        announcement.Status.ShouldBe(AnnouncementStatus.Open);
        var exit = announcement.JobExits.ShouldHaveSingleItem();
        exit.ActorId.ShouldBe(_helper.Id);
        exit.Note.ShouldBeNull();
        exit.OccurredAt.ShouldBe(_clock.UtcNow);
    }

    [Fact]
    public async Task AbandonJob_OtherWithoutNote_Throws()
    {
        var announcement = await AcceptedAnnouncementAsync();

        await Should.ThrowAsync<InvalidJobExitException>(() => new AbandonJobHandler(_announcements, _unitOfWork, _clock)
            .HandleAsync(new AbandonJob { AnnouncementId = announcement.Id, ActorId = _owner.Id, Reason = JobExitReason.Other }));
        announcement.Status.ShouldBe(AnnouncementStatus.Accepted);
    }

    [Fact]
    public async Task InterruptJob_ByOwner_WithNote_EndsAsInterrupted_WithoutRatings_HelperResponds()
    {
        var announcement = await AcceptedAnnouncementAsync();
        announcement.Start(_helper.Id, _clock.UtcNow);

        await new InterruptJobHandler(_announcements, _unitOfWork, _clock).HandleAsync(new InterruptJob
        {
            AnnouncementId = announcement.Id, ActorId = _owner.Id, Reason = JobExitReason.Other, Note = " Zalał łazienkę. "
        });

        announcement.Status.ShouldBe(AnnouncementStatus.Interrupted);
        announcement.GetInterruption()!.Note!.Value.ShouldBe("Zalał łazienkę.");

        // F11 G: bez ocen po przerwaniu – stanowisko drugiej strony zamiast gwiazdek.
        await Should.ThrowAsync<DomainRuleException>(
            () => RateHandler().HandleAsync(RateCommand(announcement.Id, _helper.Id, 5)));
        _ratings.Ratings.ShouldBeEmpty();

        await new RespondToJobExitHandler(_announcements, _unitOfWork, _clock).HandleAsync(new RespondToJobExit
        {
            AnnouncementId = announcement.Id, Number = 1, ActorId = _helper.Id, Text = "  Łazienkę zalał wąż, nie ja. "
        });
        var exit = announcement.GetInterruption()!;
        exit.Response!.Value.ShouldBe("Łazienkę zalał wąż, nie ja.");
        exit.RespondedAt.ShouldBe(_clock.UtcNow);
    }

    [Fact]
    public async Task InterruptJob_WhenAccepted_Throws()
    {
        var announcement = await AcceptedAnnouncementAsync();

        await Should.ThrowAsync<DomainRuleException>(() => new InterruptJobHandler(_announcements, _unitOfWork, _clock)
            .HandleAsync(new InterruptJob { AnnouncementId = announcement.Id, ActorId = _owner.Id, Reason = JobExitReason.NoContact }));
    }

    // ---------- ocena ----------

    [Fact]
    public async Task Rate_ByOwnerAfterCompletion_AddsRatingForHelper()
    {
        var announcement = await CompletedAnnouncementAsync();

        await RateHandler().HandleAsync(RateCommand(announcement.Id, _owner.Id, 5, "Polecam!"));

        var rating = _ratings.Ratings.ShouldHaveSingleItem();
        rating.RaterId.ShouldBe(_owner.Id);
        rating.RateeId.ShouldBe(_helper.Id);
        rating.Rate.Value.ShouldBe(5);
        rating.Comment!.Value.ShouldBe("Polecam!");
        rating.CreatedAt.ShouldBe(_clock.UtcNow);
    }

    [Fact]
    public async Task Rate_BothSides_AddsTwoRatings()
    {
        var announcement = await CompletedAnnouncementAsync();

        await RateHandler().HandleAsync(RateCommand(announcement.Id, _owner.Id, 5));
        await RateHandler().HandleAsync(RateCommand(announcement.Id, _helper.Id, 4));

        _ratings.Ratings.Count.ShouldBe(2);
        _ratings.Ratings.Single(r => r.RaterId == _helper.Id).RateeId.ShouldBe(_owner.Id);
    }

    [Fact]
    public async Task Rate_Twice_Throws()
    {
        var announcement = await CompletedAnnouncementAsync();
        await RateHandler().HandleAsync(RateCommand(announcement.Id, _owner.Id, 5));

        await Should.ThrowAsync<AlreadyRatedException>(
            () => RateHandler().HandleAsync(RateCommand(announcement.Id, _owner.Id, 1)));
        _ratings.Ratings.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Rate_BeforeCompletion_Throws()
    {
        var announcement = await AcceptedAnnouncementAsync();

        await Should.ThrowAsync<DomainRuleException>(
            () => RateHandler().HandleAsync(RateCommand(announcement.Id, _owner.Id, 5)));
        _ratings.Ratings.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rate_OutOfRange_Throws()
    {
        var announcement = await CompletedAnnouncementAsync();

        await Should.ThrowAsync<DomainRuleException>(
            () => RateHandler().HandleAsync(RateCommand(announcement.Id, _owner.Id, 6)));
    }

    // ---------- pomocnicze ----------

    private User AddUser(string userName, string email)
    {
        var user = new User(UserId.New(), userName, "Test User", FakePasswordManager.Prefix + "Haslo123!",
            new ContactData(email, "+48600100200", "Polska", "Kraków", "30-001"), _clock.UtcNow);
        _users.Users.Add(user);
        return user;
    }

    private CreateAnnouncementHandler CreateHandler()
        => new(_announcements, _categories, _users, _unitOfWork, _clock);

    private RateAnnouncementHandler RateHandler() => new(_announcements, _ratings, _unitOfWork, _clock);

    private CreateAnnouncement CreateCommand() => new()
    {
        AnnouncementId = Guid.NewGuid(),
        OwnerId = _owner.Id,
        CategoryId = _category.Id,
        SubcategoryId = _subcategoryId,
        Difficulty = DifficultyLevel.Easy,
        Title = "Cieknący kran",
        Description = "Kran w kuchni cieknie od tygodnia.",
        Budget = new PriceDto { Amount = 150, Currency = "pln" },
        Location = new LocationDto { Latitude = 50.06, Longitude = 19.94 }
    };

    private MakeOffer OfferCommand(Guid announcementId) => new()
    {
        AnnouncementId = announcementId,
        OfferId = Guid.NewGuid(),
        HelperId = _helper.Id,
        Price = new PriceDto { Amount = 120, Currency = "PLN" },
        ProposedStart = _clock.UtcNow.AddDays(1),
        ProposedEnd = _clock.UtcNow.AddDays(1).AddHours(2),
        Message = "Mogę wpaść po pracy."
    };

    private static RateAnnouncement RateCommand(Guid announcementId, Guid raterId, int rate, string? comment = null)
        => new() { AnnouncementId = announcementId, RatingId = Guid.NewGuid(), RaterId = raterId, Rate = rate, Comment = comment };

    private async Task<Announcement> CreateAnnouncementAsync()
    {
        await CreateHandler().HandleAsync(CreateCommand());
        return _announcements.Announcements.Last();
    }

    private async Task<Guid> MakeOfferAsync(Announcement announcement)
    {
        var command = OfferCommand(announcement.Id);
        await new MakeOfferHandler(_announcements, _users, _conversations, _unitOfWork, _clock).HandleAsync(command);
        return command.OfferId;
    }

    private async Task<Announcement> AcceptedAnnouncementAsync()
    {
        var announcement = await CreateAnnouncementAsync();
        var offerId = await MakeOfferAsync(announcement);
        await new AcceptOfferHandler(_announcements, _unitOfWork, _clock)
            .HandleAsync(new AcceptOffer(announcement.Id, offerId, _owner.Id));
        return announcement;
    }

    private async Task<Announcement> CompletedAnnouncementAsync()
    {
        var announcement = await AcceptedAnnouncementAsync();
        announcement.Start(_helper.Id, _clock.UtcNow);
        announcement.Complete(_owner.Id, _clock.UtcNow);
        return announcement;
    }
}
