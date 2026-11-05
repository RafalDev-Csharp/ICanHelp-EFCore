using ICanHelp.Application.Exceptions;
using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

internal static class RepositoryExtensions
{
    public static async Task<Announcement> GetOrThrowAsync(this IAnnouncementRepository announcements,
        Guid announcementId, CancellationToken cancellationToken)
        => await announcements.GetByIdAsync(new Id(announcementId), cancellationToken)
           ?? throw new AnnouncementNotFoundException(announcementId);

    /// <summary>Użytkownik z tokenu musi istnieć i nie może być zablokowany, żeby tworzyć nowe rzeczy.</summary>
    public static async Task<User> GetActiveOrThrowAsync(this IUserRepository users, Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(new UserId(userId), cancellationToken)
                   ?? throw new UserNotFoundException(userId);

        if (user.IsLocked)
        {
            throw new UserLockedException();
        }

        return user;
    }
}
