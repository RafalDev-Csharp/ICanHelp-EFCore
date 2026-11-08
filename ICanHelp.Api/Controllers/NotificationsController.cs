using ICanHelp.Api.Security;
using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Commands;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ICanHelp.Api.Controllers;

/// <summary>
/// Powiadomienia zalogowanego użytkownika (Krok 13): nowa oferta / kontroferta / akceptacja / odrzucenie / wycofanie,
/// nowa wiadomość, edycja ogłoszenia, o które negocjuje. Powstają w tle (outbox) chwilę po zdarzeniu;
/// na żywo przychodzą też przez SignalR (<c>/hubs/notifications</c>). Cudze powiadomienia → 404.
/// </summary>
[ApiController]
[Route("api/me/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    /// <summary>Moje powiadomienia od najnowszych; <c>unreadOnly=true</c> – tylko nieprzeczytane.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NotificationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetMine(
        [FromServices] IQueryHandler<GetMyNotifications, IReadOnlyList<NotificationDto>> handler,
        CancellationToken cancellationToken,
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var notifications = await handler.HandleAsync(new GetMyNotifications
        {
            UserId = userId,
            UnreadOnly = unreadOnly,
            Page = page,
            PageSize = pageSize
        }, cancellationToken);

        return Ok(notifications);
    }

    /// <summary>Liczba nieprzeczytanych (plakietka).</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType<UnreadCountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount(
        [FromServices] IQueryHandler<GetUnreadNotificationCount, UnreadCountDto> handler,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await handler.HandleAsync(new GetUnreadNotificationCount { UserId = userId }, cancellationToken));
    }

    /// <summary>Oznacza jedno powiadomienie jako przeczytane (ponownie – bez zmian). Cudze albo nieistniejące → 404.</summary>
    [HttpPost("{notificationId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(
        Guid notificationId,
        [FromServices] ICommandHandler<MarkNotificationAsRead> handler,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await handler.HandleAsync(new MarkNotificationAsRead(notificationId, userId), cancellationToken);
        return NoContent();
    }

    /// <summary>Oznacza wszystkie moje powiadomienia jako przeczytane.</summary>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllAsRead(
        [FromServices] ICommandHandler<MarkAllNotificationsAsRead> handler,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await handler.HandleAsync(new MarkAllNotificationsAsRead(userId), cancellationToken);
        return NoContent();
    }
}
