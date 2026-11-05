using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Time;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

internal sealed class MakeOfferHandler : ICommandHandler<MakeOffer>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public MakeOfferHandler(IAnnouncementRepository announcements, IUserRepository users, IUnitOfWork unitOfWork,
        IClock clock)
    {
        _announcements = announcements;
        _users = users;
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

            announcement.AddOffer(new Id(command.OfferId), new UserId(command.HelperId), price,
                command.ProposedStart.AsUtc(), command.ProposedEnd.AsUtc(), message, _clock.UtcNow);
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

internal sealed class AcceptOfferHandler : ICommandHandler<AcceptOffer>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly IUnitOfWork _unitOfWork;

    public AcceptOfferHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork)
    {
        _announcements = announcements;
        _unitOfWork = unitOfWork;
    }

    public Task HandleAsync(AcceptOffer command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await _announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.AcceptOffer(new Id(command.OfferId), new UserId(command.ActorId));
        }, cancellationToken);
}

internal sealed class RejectOfferHandler : ICommandHandler<RejectOffer>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly IUnitOfWork _unitOfWork;

    public RejectOfferHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork)
    {
        _announcements = announcements;
        _unitOfWork = unitOfWork;
    }

    public Task HandleAsync(RejectOffer command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await _announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.RejectOffer(new Id(command.OfferId), new UserId(command.ActorId));
        }, cancellationToken);
}

internal sealed class WithdrawOfferHandler : ICommandHandler<WithdrawOffer>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly IUnitOfWork _unitOfWork;

    public WithdrawOfferHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork)
    {
        _announcements = announcements;
        _unitOfWork = unitOfWork;
    }

    public Task HandleAsync(WithdrawOffer command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await _announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.WithdrawOffer(new Id(command.OfferId), new UserId(command.ActorId));
        }, cancellationToken);
}

internal static class OfferMessage
{
    /// <summary>Wiadomość jest opcjonalna – pusty tekst = brak wiadomości.</summary>
    public static Description? From(string? message)
        => string.IsNullOrWhiteSpace(message) ? null : new Description(message);
}
