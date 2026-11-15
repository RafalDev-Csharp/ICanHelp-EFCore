using ICanHelp.Application.Exceptions;
using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Alerts;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Entities.Notifications;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

/// <summary>Kroki, które powtarzają się w handlerach: „wczytaj albo 404”, „moje albo 404”, sprawdzenie kategorii.</summary>
internal static class RepositoryExtensions
{
    public static async Task<Announcement> GetOrThrowAsync(this IAnnouncementRepository announcements,
        Guid announcementId, CancellationToken cancellationToken)
        => await announcements.GetByIdAsync(new Id(announcementId), cancellationToken)
           ?? throw new AnnouncementNotFoundException(announcementId);

    public static async Task<Conversation> GetOrThrowAsync(this IConversationRepository conversations,
        Guid conversationId, CancellationToken cancellationToken)
        => await conversations.GetByIdAsync(new Id(conversationId), cancellationToken)
           ?? throw new ConversationNotFoundException(conversationId);

    /// <summary>
    /// Wątek pomocnika w ogłoszeniu: istniejący albo nowy (<paramref name="newConversationId"/>) – jeden na parę
    /// (ogłoszenie, pomocnik). Nowy zakłada domena (sprawdza status Open i że to nie właściciel).
    /// </summary>
    public static async Task<Conversation> GetOrStartAsync(this IConversationRepository conversations,
        Announcement announcement, UserId helperId, Id newConversationId, DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var existing = await conversations.GetByParticipantsAsync(announcement.Id, helperId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var conversation = announcement.StartConversation(newConversationId, helperId, nowUtc);
        await conversations.AddAsync(conversation, cancellationToken);
        return conversation;
    }

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

    // ---------- zasoby z api/me/*: cudzy wygląda jak nieistniejący (404) – nie zdradzamy, że istnieje ----------

    public static async Task<AnnouncementAlert> GetOwnedOrThrowAsync(this IAnnouncementAlertRepository alerts,
        Guid alertId, UserId userId, CancellationToken cancellationToken)
    {
        var alert = await alerts.GetByIdAsync(new Id(alertId), cancellationToken);
        return alert is not null && alert.IsOwnedBy(userId) ? alert : throw new AlertNotFoundException(alertId);
    }

    public static async Task<UserDevice> GetOwnedOrThrowAsync(this IUserDeviceRepository devices,
        Guid deviceId, UserId userId, CancellationToken cancellationToken)
    {
        var device = await devices.GetByIdAsync(new Id(deviceId), cancellationToken);
        return device is not null && device.IsOwnedBy(userId) ? device : throw new DeviceNotFoundException(deviceId);
    }

    public static async Task<Notification> GetOwnedOrThrowAsync(this INotificationRepository notifications,
        Guid notificationId, UserId userId, CancellationToken cancellationToken)
    {
        var notification = await notifications.GetByIdAsync(new Id(notificationId), cancellationToken);
        return notification is not null && notification.UserId == userId
            ? notification
            : throw new NotificationNotFoundException(notificationId);
    }

    /// <summary>
    /// Kategoria musi istnieć, a każda z podanych podkategorii do niej należeć (inaczej 400 <c>InvalidCategory</c>).
    /// Ogłoszenie podaje jedną podkategorię, alert – listę (pusta = cała kategoria).
    /// </summary>
    public static async Task EnsureCategoryAsync(this ICategoryRepository categories, Id categoryId,
        IReadOnlyCollection<Id> subcategoryIds, CancellationToken cancellationToken)
    {
        var category = await categories.GetByIdAsync(categoryId, cancellationToken);
        if (category is null)
        {
            throw new InvalidCategoryException(categoryId, subcategoryIds.FirstOrDefault()?.Value ?? Guid.Empty);
        }

        var wrong = subcategoryIds.FirstOrDefault(s => !category.HasSubcategory(s));
        if (wrong is not null)
        {
            throw new InvalidCategoryException(categoryId, wrong);
        }
    }
}
