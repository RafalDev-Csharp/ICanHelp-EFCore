using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Time;
using ICanHelp.Core.Entities;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

internal sealed class CreateAnnouncementHandler : ICommandHandler<CreateAnnouncement>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly ICategoryRepository _categories;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CreateAnnouncementHandler(IAnnouncementRepository announcements, ICategoryRepository categories,
        IUserRepository users, IUnitOfWork unitOfWork, IClock clock)
    {
        _announcements = announcements;
        _categories = categories;
        _users = users;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task HandleAsync(CreateAnnouncement command, CancellationToken cancellationToken = default)
    {
        var categoryId = new Id(command.CategoryId);
        var subcategoryId = new Id(command.SubcategoryId);

        await _unitOfWork.ExecuteAsync(async () =>
        {
            await _users.GetActiveOrThrowAsync(command.OwnerId, cancellationToken);

            var category = await _categories.GetByIdAsync(categoryId, cancellationToken);
            if (category is null || !category.HasSubcategory(subcategoryId))
            {
                throw new InvalidCategoryException(command.CategoryId, command.SubcategoryId);
            }

            var announcement = new Announcement(
                new Id(command.AnnouncementId),
                new UserId(command.OwnerId),
                categoryId,
                subcategoryId,
                command.Difficulty,
                new StringObject(command.Title),
                new Description(command.Description),
                new Price(command.Budget.Amount, command.Budget.Currency),
                new Location(command.Location.Latitude, command.Location.Longitude),
                _clock.UtcNow);

            await _announcements.AddAsync(announcement, cancellationToken);
        }, cancellationToken);
    }
}
