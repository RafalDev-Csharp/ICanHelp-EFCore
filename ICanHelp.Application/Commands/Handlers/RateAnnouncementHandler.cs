using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Time;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

internal sealed class RateAnnouncementHandler(IAnnouncementRepository announcements, IRatingRepository ratings,
    IUnitOfWork unitOfWork, IClock clock) : ICommandHandler<RateAnnouncement>
{
    public async Task HandleAsync(RateAnnouncement command, CancellationToken cancellationToken = default)
    {
        var raterId = new UserId(command.RaterId);
        var rate = new Rate(command.Rate);
        var comment = command.Comment.ToOptionalDescription();

        await unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);

            // Domena sprawdza status (Completed) i uczestnika, i sama wybiera ocenianego (drugą stronę).
            var rating = announcement.CreateRating(new Id(command.RatingId), raterId, rate, comment, clock.UtcNow);

            if (await ratings.ExistsAsync(announcement.Id, raterId, cancellationToken))
            {
                throw new AlreadyRatedException(command.AnnouncementId);
            }

            await ratings.AddAsync(rating, cancellationToken);
        }, cancellationToken);
    }
}
