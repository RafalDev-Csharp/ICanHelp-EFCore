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
internal sealed class UpdateAnnouncementHandler(IAnnouncementRepository announcements, ICategoryRepository categories,
    IUserRepository users, IUnitOfWork unitOfWork, IClock clock) : ICommandHandler<UpdateAnnouncement>
{
    public Task HandleAsync(UpdateAnnouncement command, CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async () =>
        {
            await users.GetActiveOrThrowAsync(command.ActorId, cancellationToken);
            var announcement = await announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);

            // Klient edytował starszą wersję – nie nadpisujemy po cichu cudzej (albo własnej z innej karty) zmiany.
            if (command.ExpectedRevision is { } expectedRevision && expectedRevision != announcement.Revision)
            {
                throw new ConcurrencyConflictException();
            }

            var categoryId = command.CategoryId is { } newCategoryId ? new Id(newCategoryId) : announcement.CategoryId;
            var subcategoryId = command.SubcategoryId is { } newSubcategoryId
                ? new Id(newSubcategoryId)
                : announcement.SubcategoryId;

            if (categoryId != announcement.CategoryId || subcategoryId != announcement.SubcategoryId)
            {
                await categories.EnsureCategoryAsync(categoryId, [subcategoryId], cancellationToken);
            }

            announcement.Update(
                new UserId(command.ActorId),
                categoryId,
                subcategoryId,
                command.Difficulty ?? announcement.Difficulty,
                command.Title is null ? announcement.Title : new StringObject(command.Title),
                command.Description is null ? announcement.Description : new Description(command.Description),
                command.Budget?.ToPrice() ?? announcement.Budget,
                command.Location?.ToLocation() ?? announcement.Location,
                clock.UtcNow);
        }, cancellationToken);
}
