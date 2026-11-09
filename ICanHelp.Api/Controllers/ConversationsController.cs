using ICanHelp.Api.Security;
using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Commands;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ICanHelp.Api.Controllers;

/// <summary>
/// Wątki negocjacji: rozmowa właściciela ogłoszenia z jednym pomocnikiem. Tylko dla tych dwóch osób.
/// Wątek zakłada się przez <c>POST api/announcements/{id}/conversations</c> (albo automatycznie przy pierwszej ofercie).
/// </summary>
[ApiController]
[Route("api/conversations")]
[Authorize]
public sealed class ConversationsController : ControllerBase
{
    /// <summary>
    /// „Moje negocjacje”: wątki zalogowanego użytkownika (jako właściciel ogłoszenia albo pomocnik) z podglądem
    /// ogłoszenia, ostatniej oferty i wiadomości, licznikiem nieprzeczytanych i flagą zmiany ogłoszenia –
    /// od ostatniej aktywności. Filtry: <c>role</c> (Client / Helper), <c>status</c> ogłoszenia (np. Open),
    /// <c>announcementId</c> (wątki jednego ogłoszenia – właściciel widzi przy każdej ofercie link do rozmowy).
    /// Do odpytywania (polling), dopóki nie ma powiadomień (Krok 13).
    /// </summary>
    [HttpGet("~/api/me/conversations")]
    [ProducesResponseType<IReadOnlyList<MyConversationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<MyConversationDto>>> GetMine(
        [FromQuery] OfferParty? role,
        [FromQuery] AnnouncementStatus? status,
        [FromQuery] Guid? announcementId,
        [FromServices] IQueryHandler<GetMyConversations, IReadOnlyList<MyConversationDto>> handler,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var conversations = await handler.HandleAsync(new GetMyConversations
        {
            UserId = userId,
            Role = role,
            Status = status,
            AnnouncementId = announcementId,
            Page = page,
            PageSize = pageSize
        }, cancellationToken);

        return Ok(conversations);
    }

    /// <summary>
    /// Wątek z osią czasu: wiadomości, oferty tego pomocnika i zmiany ogłoszenia (wpisy „AnnouncementChanged”),
    /// od najstarszych. Dla osób spoza wątku: 404.
    /// </summary>
    [HttpGet("{conversationId:guid}")]
    [ProducesResponseType<ConversationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationDto>> GetById(
        Guid conversationId,
        [FromServices] IQueryHandler<GetConversation, ConversationDto?> handler,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var conversation = await handler.HandleAsync(
            new GetConversation { ConversationId = conversationId, UserId = userId }, cancellationToken);

        return conversation is null ? NotFound() : Ok(conversation);
    }

    /// <summary>
    /// Wiadomość (2–500 znaków). Pisać można, dopóki ogłoszenie jest otwarte; po akceptacji – tylko w wątku
    /// zaakceptowanego pomocnika. Inaczej (albo dla osoby spoza wątku) 409. Zwraca id wiadomości.
    /// </summary>
    [HttpPost("{conversationId:guid}/messages")]
    [ProducesResponseType<CreatedDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedDto>> PostMessage(
        Guid conversationId,
        [FromBody] SendMessage command,
        [FromServices] ICommandHandler<SendMessage> handler,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var messageId = Guid.NewGuid();
        await handler.HandleAsync(
            command with { ConversationId = conversationId, MessageId = messageId, SenderId = userId }, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { conversationId }, new CreatedDto(messageId));
    }

    /// <summary>Oznacza wątek jako przeczytany do teraz (<c>unreadCount</c> = 0). Tylko uczestnik, inaczej 409.</summary>
    [HttpPost("{conversationId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkAsRead(
        Guid conversationId,
        [FromServices] ICommandHandler<MarkConversationAsRead> handler,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await handler.HandleAsync(new MarkConversationAsRead(conversationId, userId), cancellationToken);
        return NoContent();
    }
}
