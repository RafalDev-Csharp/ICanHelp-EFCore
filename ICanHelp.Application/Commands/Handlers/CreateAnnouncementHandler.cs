using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Time;
using ICanHelp.Core.Entities;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

internal sealed class CreateAnnouncementHandler(IAnnouncementRepository announcements, ICategoryRepository categories,
    IUserRepository users, IUnitOfWork unitOfWork, IClock clock) : ICommandHandler<CreateAnnouncement>
{
    public async Task HandleAsync(CreateAnnouncement command, CancellationToken cancellationToken = default)
    {
        var categoryId = new Id(command.CategoryId);
        var subcategoryId = new Id(command.SubcategoryId);

        await unitOfWork.ExecuteAsync(async () =>
        {
            await users.GetActiveOrThrowAsync(command.OwnerId, cancellationToken);
            await categories.EnsureCategoryAsync(categoryId, [subcategoryId], cancellationToken);

            var announcement = new Announcement(
                new Id(command.AnnouncementId),
                new UserId(command.OwnerId),
                categoryId,
                subcategoryId,
                command.Difficulty,
                new StringObject(command.Title),
                new Description(command.Description),
                command.Budget.ToPrice(),
                command.Location.ToLocation(),
                clock.UtcNow);

            await announcements.AddAsync(announcement, cancellationToken);
        }, cancellationToken);
    }
}
