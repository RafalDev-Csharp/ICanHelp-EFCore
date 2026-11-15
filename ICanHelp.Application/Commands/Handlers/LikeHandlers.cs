using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Time;
using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

// Polubienie = istnienie wiersza (UserId, AnnouncementId). Obie komendy są idempotentne (PUT/DELETE).

internal sealed class LikeAnnouncementHandler(ILikedAnnouncementRepository likes, IAnnouncementRepository announcements,
    IUserRepository users, IUnitOfWork unitOfWork, IClock clock) : ICommandHandler<LikeAnnouncement>
{
    public Task HandleAsync(LikeAnnouncement command, CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async () =>
        {
            await users.GetActiveOrThrowAsync(command.UserId, cancellationToken);
            var announcement = await announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);

            var userId = new UserId(command.UserId);
            if (await likes.GetAsync(userId, announcement.Id, cancellationToken) is not null)
            {
                return;
            }

            await likes.AddAsync(new LikedAnnouncement(userId, announcement.Id, clock.UtcNow), cancellationToken);
        }, cancellationToken);
}

internal sealed class UnlikeAnnouncementHandler(ILikedAnnouncementRepository likes, IUnitOfWork unitOfWork)
    : ICommandHandler<UnlikeAnnouncement>
{
    public Task HandleAsync(UnlikeAnnouncement command, CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async () =>
        {
            var like = await likes.GetAsync(new UserId(command.UserId), new Id(command.AnnouncementId),
                cancellationToken);
            if (like is not null)
            {
                likes.Remove(like);
            }
        }, cancellationToken);
}
