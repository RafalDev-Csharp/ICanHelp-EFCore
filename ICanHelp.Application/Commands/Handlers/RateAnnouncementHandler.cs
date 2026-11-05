using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Time;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

internal sealed class RateAnnouncementHandler : ICommandHandler<RateAnnouncement>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly IRatingRepository _ratings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RateAnnouncementHandler(IAnnouncementRepository announcements, IRatingRepository ratings,
        IUnitOfWork unitOfWork, IClock clock)
    {
        _announcements = announcements;
        _ratings = ratings;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task HandleAsync(RateAnnouncement command, CancellationToken cancellationToken = default)
    {
        var raterId = new UserId(command.RaterId);
        var rate = new Rate(command.Rate);
        var comment = string.IsNullOrWhiteSpace(command.Comment) ? null : new Description(command.Comment);

        await _unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await _announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);

            // Domena sprawdza status (Completed) i uczestnika, i sama wybiera ocenianego (drugą stronę).
            var rating = announcement.CreateRating(new Id(command.RatingId), raterId, rate, comment, _clock.UtcNow);

            if (await _ratings.ExistsAsync(announcement.Id, raterId, cancellationToken))
            {
                throw new AlreadyRatedException(command.AnnouncementId);
            }

            await _ratings.AddAsync(rating, cancellationToken);
        }, cancellationToken);
    }
}
