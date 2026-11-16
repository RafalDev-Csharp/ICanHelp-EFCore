using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Time;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

// Warunki oferty (cena, terminy, wiadomość) sprawdzamy przed transakcją – błędne dane nie dotykają bazy.

internal sealed class MakeOfferHandler(IAnnouncementRepository announcements, IUserRepository users,
    IConversationRepository conversations, IUnitOfWork unitOfWork, IClock clock) : ICommandHandler<MakeOffer>
{
    public async Task HandleAsync(MakeOffer command, CancellationToken cancellationToken = default)
    {
        var terms = command.ToTerms();

        await unitOfWork.ExecuteAsync(async () =>
        {
            await users.GetActiveOrThrowAsync(command.HelperId, cancellationToken);
            var announcement = await announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);

            var now = clock.UtcNow;
            var helperId = new UserId(command.HelperId);

            announcement.AddOffer(new Id(command.OfferId), helperId, terms, now);

            // Oferta bez rozmowy nie istnieje: pierwszy krok negocjacji zakłada wątek (jeśli pomocnik nie pytał wcześniej).
            await conversations.GetOrStartAsync(announcement, helperId, Id.New(), now, cancellationToken);
        }, cancellationToken);
    }
}

internal sealed class CounterOfferHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork, IClock clock)
    : ICommandHandler<CounterOffer>
{
    public async Task HandleAsync(CounterOffer command, CancellationToken cancellationToken = default)
    {
        var terms = command.ToTerms();

        await unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.CounterOffer(new Id(command.OfferId), new Id(command.NewOfferId), new UserId(command.ActorId),
                terms, clock.UtcNow);
        }, cancellationToken);
    }
}

internal sealed class ReviseOfferHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork, IClock clock)
    : ICommandHandler<ReviseOffer>
{
    public async Task HandleAsync(ReviseOffer command, CancellationToken cancellationToken = default)
    {
        var terms = command.ToTerms();

        await unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.ReviseOffer(new Id(command.OfferId), new Id(command.NewOfferId), new UserId(command.ActorId),
                terms, clock.UtcNow);
        }, cancellationToken);
    }
}

internal sealed class AcceptOfferHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork, IClock clock)
    : ICommandHandler<AcceptOffer>
{
    public Task HandleAsync(AcceptOffer command, CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.AcceptOffer(new Id(command.OfferId), new UserId(command.ActorId), clock.UtcNow);
        }, cancellationToken);
}

internal sealed class RejectOfferHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork, IClock clock)
    : ICommandHandler<RejectOffer>
{
    public Task HandleAsync(RejectOffer command, CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.RejectOffer(new Id(command.OfferId), new UserId(command.ActorId), clock.UtcNow);
        }, cancellationToken);
}

internal sealed class WithdrawOfferHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork, IClock clock)
    : ICommandHandler<WithdrawOffer>
{
    public Task HandleAsync(WithdrawOffer command, CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.WithdrawOffer(new Id(command.OfferId), new UserId(command.ActorId), clock.UtcNow);
        }, cancellationToken);
}
