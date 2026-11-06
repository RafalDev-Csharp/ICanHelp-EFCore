using ICanHelp.Application.Exceptions;
using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Negotiations;
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
}
