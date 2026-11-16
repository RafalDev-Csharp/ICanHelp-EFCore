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
[Route("api/me/notifications")]
[Authorize]
public sealed class NotificationsController : ApiController
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
        var notifications = await handler.HandleAsync(new GetMyNotifications
        {
            UserId = CurrentUserId,
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
        => Ok(await handler.HandleAsync(new GetUnreadNotificationCount { UserId = CurrentUserId }, cancellationToken));

    /// <summary>Oznacza jedno powiadomienie jako przeczytane (ponownie – bez zmian). Cudze albo nieistniejące → 404.</summary>
    [HttpPost("{notificationId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> MarkAsRead(
        Guid notificationId,
        [FromServices] ICommandHandler<MarkNotificationAsRead> handler,
        CancellationToken cancellationToken)
        => Send(new MarkNotificationAsRead(notificationId, CurrentUserId), handler, cancellationToken);

    /// <summary>
    /// Oznacza wszystkie moje powiadomienia jako przeczytane; z <c>conversationId</c> – tylko te z jednego wątku
    /// (aplikacja otworzyła wątek, więc jego wiadomości i oferty są widziane). Cudzy wątek – nic się nie zmienia.
    /// </summary>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> MarkAllAsRead(
        [FromServices] ICommandHandler<MarkAllNotificationsAsRead> handler,
        CancellationToken cancellationToken,
        [FromQuery] Guid? conversationId = null)
        => Send(new MarkAllNotificationsAsRead(CurrentUserId, conversationId), handler, cancellationToken);
}
