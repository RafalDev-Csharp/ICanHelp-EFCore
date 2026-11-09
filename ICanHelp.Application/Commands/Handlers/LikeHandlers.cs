using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Time;
using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

// Polubienie = istnienie wiersza (UserId, AnnouncementId). Obie komendy są idempotentne (PUT/DELETE).

internal sealed class LikeAnnouncementHandler : ICommandHandler<LikeAnnouncement>
{
    private readonly ILikedAnnouncementRepository _likes;
    private readonly IAnnouncementRepository _announcements;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public LikeAnnouncementHandler(ILikedAnnouncementRepository likes, IAnnouncementRepository announcements,
        IUserRepository users, IUnitOfWork unitOfWork, IClock clock)
    {
        _likes = likes;
        _announcements = announcements;
        _users = users;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task HandleAsync(LikeAnnouncement command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            await _users.GetActiveOrThrowAsync(command.UserId, cancellationToken);
            var announcement = await _announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);

            var userId = new UserId(command.UserId);
            if (await _likes.GetAsync(userId, announcement.Id, cancellationToken) is not null)
            {
                return;
            }

            await _likes.AddAsync(new LikedAnnouncement(userId, announcement.Id, _clock.UtcNow), cancellationToken);
        }, cancellationToken);
}

internal sealed class UnlikeAnnouncementHandler : ICommandHandler<UnlikeAnnouncement>
{
    private readonly ILikedAnnouncementRepository _likes;
    private readonly IUnitOfWork _unitOfWork;

    public UnlikeAnnouncementHandler(ILikedAnnouncementRepository likes, IUnitOfWork unitOfWork)
    {
        _likes = likes;
        _unitOfWork = unitOfWork;
    }

    public Task HandleAsync(UnlikeAnnouncement command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var like = await _likes.GetAsync(new UserId(command.UserId), new Id(command.AnnouncementId),
                cancellationToken);
            if (like is not null)
            {
                _likes.Remove(like);
            }
        }, cancellationToken);
}
