using ICanHelp.Api.Security;
using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Commands;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ICanHelp.Api.Controllers;

/// <summary>
/// Alerty o nowych ogłoszeniach (Krok 14): „daj znać o nowych ogłoszeniach z Hydrauliki do 5 km od domu”
/// (<c>kind: Area</c> + <c>center</c>) albo „do 5 km od mojej drogi z pracy” (<c>kind: Route</c> + <c>routePoints</c> –
/// polilinia z serwisu routingu, np. OSRM, po stronie aplikacji). Pasujące ogłoszenie → powiadomienie
/// <c>NewAnnouncementNearby</c> w <c>api/me/notifications</c>. Max 10 alertów; cudze alerty → 404.
/// </summary>
[ApiController]
[Route("api/me/alerts")]
[Authorize]
public sealed class AlertsController : ControllerBase
{
    /// <summary>Moje alerty, od najnowszych.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AlertDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<AlertDto>>> GetMine(
        [FromServices] IQueryHandler<GetMyAlerts, IReadOnlyList<AlertDto>> handler,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await handler.HandleAsync(new GetMyAlerts { UserId = userId }, cancellationToken));
    }

    [HttpGet("{alertId:guid}")]
    [ProducesResponseType<AlertDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlertDto>> GetById(
        Guid alertId,
        [FromServices] IQueryHandler<GetMyAlert, AlertDto?> handler,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var alert = await handler.HandleAsync(new GetMyAlert { AlertId = alertId, UserId = userId }, cancellationToken);
        return alert is null ? NotFound() : Ok(alert);
    }

    /// <summary>
    /// Podgląd: otwarte ogłoszenia, które pasują do alertu teraz (od najnowszych, z <c>distanceKm</c> od punktu / trasy).
    /// Pomaga dobrać promień. <c>limit</c> 1–100, domyślnie 20.
    /// </summary>
    [HttpGet("{alertId:guid}/preview")]
    [ProducesResponseType<IReadOnlyList<AnnouncementDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AnnouncementDto>>> Preview(
        Guid alertId,
        [FromServices] IQueryHandler<GetAlertPreview, IReadOnlyList<AnnouncementDto>?> handler,
        CancellationToken cancellationToken,
        [FromQuery] int limit = 20)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var announcements = await handler.HandleAsync(
            new GetAlertPreview { AlertId = alertId, UserId = userId, Limit = limit }, cancellationToken);

        return announcements is null ? NotFound() : Ok(announcements);
    }

    /// <summary>
    /// Nowy alert (włączony). Promień 0,5–50 km, trasa 2–1000 punktów, puste <c>subcategoryIds</c> = cała kategoria,
    /// <c>schedule</c> opcjonalnie (np. <c>{"days":"Workdays","from":"15:00:00","to":"18:00:00"}</c>).
    /// 11. alert → 409.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<CreatedDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedDto>> Create(
        [FromBody] CreateAlert command,
        [FromServices] ICommandHandler<CreateAlert> handler,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var alertId = Guid.NewGuid();
        await handler.HandleAsync(command with { AlertId = alertId, UserId = userId }, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { alertId }, new CreatedDto(alertId));
    }

    /// <summary>
    /// Częściowa zmiana (pominięte / <c>null</c> = bez zmian): nazwa, kategoria, podkategorie, punkt / trasa, promień,
    /// harmonogram. Rodzaju (Area/Route) zmienić się nie da. Nowe punkty albo promień przeliczają strefę.
    /// </summary>
    [HttpPatch("{alertId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid alertId,
        [FromBody] UpdateAlert command,
        [FromServices] ICommandHandler<UpdateAlert> handler,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await handler.HandleAsync(command with { AlertId = alertId, UserId = userId }, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{alertId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid alertId,
        [FromServices] ICommandHandler<DeleteAlert> handler,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await handler.HandleAsync(new DeleteAlert(alertId, userId), cancellationToken);
        return NoContent();
    }

    /// <summary>Włącza alert (ponownie – bez zmian).</summary>
    [HttpPost("{alertId:guid}/enable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Enable(
        Guid alertId,
        [FromServices] ICommandHandler<SetAlertEnabled> handler,
        CancellationToken cancellationToken)
        => SetEnabledAsync(alertId, true, handler, cancellationToken);

    /// <summary>Wyłącza alert – zostaje na liście, ale nie wysyła powiadomień.</summary>
    [HttpPost("{alertId:guid}/disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Disable(
        Guid alertId,
        [FromServices] ICommandHandler<SetAlertEnabled> handler,
        CancellationToken cancellationToken)
        => SetEnabledAsync(alertId, false, handler, cancellationToken);

    private async Task<IActionResult> SetEnabledAsync(Guid alertId, bool isEnabled,
        ICommandHandler<SetAlertEnabled> handler, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await handler.HandleAsync(new SetAlertEnabled(alertId, userId, isEnabled), cancellationToken);
        return NoContent();
    }
}
