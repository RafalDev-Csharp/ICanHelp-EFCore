using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Time;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

internal sealed class MakeOfferHandler : ICommandHandler<MakeOffer>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly IUserRepository _users;
    private readonly IConversationRepository _conversations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public MakeOfferHandler(IAnnouncementRepository announcements, IUserRepository users,
        IConversationRepository conversations, IUnitOfWork unitOfWork, IClock clock)
    {
        _announcements = announcements;
        _users = users;
        _conversations = conversations;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task HandleAsync(MakeOffer command, CancellationToken cancellationToken = default)
    {
        var price = new Price(command.Price.Amount, command.Price.Currency);
        var message = OfferMessage.From(command.Message);

        await _unitOfWork.ExecuteAsync(async () =>
        {
            await _users.GetActiveOrThrowAsync(command.HelperId, cancellationToken);
            var announcement = await _announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);

            var now = _clock.UtcNow;
            var helperId = new UserId(command.HelperId);

            announcement.AddOffer(new Id(command.OfferId), helperId, price,
                command.ProposedStart.AsUtc(), command.ProposedEnd.AsUtc(), message, now);

            // Oferta bez rozmowy nie istnieje: pierwszy krok negocjacji zakłada wątek (jeśli pomocnik nie pytał wcześniej).
            await _conversations.GetOrStartAsync(announcement, helperId, Id.New(), now, cancellationToken);
        }, cancellationToken);
    }
}

internal sealed class CounterOfferHandler : ICommandHandler<CounterOffer>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CounterOfferHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork, IClock clock)
    {
        _announcements = announcements;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task HandleAsync(CounterOffer command, CancellationToken cancellationToken = default)
    {
        var price = new Price(command.Price.Amount, command.Price.Currency);
        var message = OfferMessage.From(command.Message);

        await _unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await _announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);

            announcement.CounterOffer(new Id(command.OfferId), new Id(command.NewOfferId), new UserId(command.ActorId),
                price, command.ProposedStart.AsUtc(), command.ProposedEnd.AsUtc(), message, _clock.UtcNow);
        }, cancellationToken);
    }
}

internal sealed class ReviseOfferHandler : ICommandHandler<ReviseOffer>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ReviseOfferHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork, IClock clock)
    {
        _announcements = announcements;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task HandleAsync(ReviseOffer command, CancellationToken cancellationToken = default)
    {
        var price = new Price(command.Price.Amount, command.Price.Currency);
        var message = OfferMessage.From(command.Message);

        await _unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await _announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);

            announcement.ReviseOffer(new Id(command.OfferId), new Id(command.NewOfferId), new UserId(command.ActorId),
                price, command.ProposedStart.AsUtc(), command.ProposedEnd.AsUtc(), message, _clock.UtcNow);
        }, cancellationToken);
    }
}

internal sealed class AcceptOfferHandler : ICommandHandler<AcceptOffer>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public AcceptOfferHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork, IClock clock)
    {
        _announcements = announcements;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task HandleAsync(AcceptOffer command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await _announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.AcceptOffer(new Id(command.OfferId), new UserId(command.ActorId), _clock.UtcNow);
        }, cancellationToken);
}

internal sealed class RejectOfferHandler : ICommandHandler<RejectOffer>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RejectOfferHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork, IClock clock)
    {
        _announcements = announcements;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task HandleAsync(RejectOffer command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await _announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.RejectOffer(new Id(command.OfferId), new UserId(command.ActorId), _clock.UtcNow);
        }, cancellationToken);
}

internal sealed class WithdrawOfferHandler : ICommandHandler<WithdrawOffer>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public WithdrawOfferHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork, IClock clock)
    {
        _announcements = announcements;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task HandleAsync(WithdrawOffer command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await _announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.WithdrawOffer(new Id(command.OfferId), new UserId(command.ActorId), _clock.UtcNow);
        }, cancellationToken);
}

internal static class OfferMessage
{
    /// <summary>Wiadomość jest opcjonalna – pusty tekst = brak wiadomości.</summary>
    public static Description? From(string? message)
        => string.IsNullOrWhiteSpace(message) ? null : new Description(message);
}
