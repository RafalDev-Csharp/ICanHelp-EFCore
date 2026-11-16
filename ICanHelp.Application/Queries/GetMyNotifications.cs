using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Queries;

/// <summary>Powiadomienia <see cref="UserId"/>, od najnowszych.</summary>
public sealed record GetMyNotifications : IQuery<IReadOnlyList<NotificationDto>>
{
    /// <summary>Kto czyta (z tokenu).</summary>
    public Guid UserId { get; init; }

    /// <summary>Tylko nieprzeczytane.</summary>
    public bool UnreadOnly { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>Licznik nieprzeczytanych powiadomień (plakietka w aplikacji).</summary>
public sealed record GetUnreadNotificationCount : IQuery<UnreadCountDto>
{
    public Guid UserId { get; init; }
}
