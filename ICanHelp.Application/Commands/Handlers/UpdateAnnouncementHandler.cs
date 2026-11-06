using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Time;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

/// <summary>
/// PATCH ogłoszenia: brakujące pola bierze z obecnego stanu, a całą resztę (właściciel, status, wykrycie zmian,
/// nowa rewizja) robi <c>Announcement.Update</c>.
/// </summary>
internal sealed class UpdateAnnouncementHandler : ICommandHandler<UpdateAnnouncement>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly ICategoryRepository _categories;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public UpdateAnnouncementHandler(IAnnouncementRepository announcements, ICategoryRepository categories,
        IUserRepository users, IUnitOfWork unitOfWork, IClock clock)
    {
        _announcements = announcements;
        _categories = categories;
        _users = users;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task HandleAsync(UpdateAnnouncement command, CancellationToken cancellationToken = default)
        => await _unitOfWork.ExecuteAsync(async () =>
        {
            await _users.GetActiveOrThrowAsync(command.ActorId, cancellationToken);
            var announcement = await _announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);

            var categoryId = command.CategoryId is { } newCategoryId ? new Id(newCategoryId) : announcement.CategoryId;
            var subcategoryId = command.SubcategoryId is { } newSubcategoryId
                ? new Id(newSubcategoryId)
                : announcement.SubcategoryId;

            if (categoryId != announcement.CategoryId || subcategoryId != announcement.SubcategoryId)
            {
                var category = await _categories.GetByIdAsync(categoryId, cancellationToken);
                if (category is null || !category.HasSubcategory(subcategoryId))
                {
                    throw new InvalidCategoryException(categoryId, subcategoryId);
                }
            }

            announcement.Update(
                new UserId(command.ActorId),
                categoryId,
                subcategoryId,
                command.Difficulty ?? announcement.Difficulty,
                command.Title is null ? announcement.Title : new StringObject(command.Title),
                command.Description is null ? announcement.Description : new Description(command.Description),
                command.Budget is null ? announcement.Budget : new Price(command.Budget.Amount, command.Budget.Currency),
                command.Location is null
                    ? announcement.Location
                    : new Location(command.Location.Latitude, command.Location.Longitude),
                _clock.UtcNow);
        }, cancellationToken);
}
