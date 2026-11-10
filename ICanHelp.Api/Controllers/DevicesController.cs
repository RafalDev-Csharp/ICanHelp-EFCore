using ICanHelp.Api.Security;
using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Commands;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ICanHelp.Api.Controllers;

/// <summary>
/// Urządzenia, które dostają powiadomienia push (Krok 15, FCM). Aplikacja po zgodzie na powiadomienia pobiera token
/// z Firebase i zgłasza go tutaj (przy każdym starcie – to odświeża urządzenie). Przy wylogowaniu / wyłączeniu
/// powiadomień – <c>DELETE</c>. Cudze urządzenia → 404.
/// </summary>
[ApiController]
[Route("api/me/devices")]
[Authorize]
public sealed class DevicesController : ControllerBase
{
    /// <summary>Moje urządzenia, od ostatnio widzianych (bez tokenów).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DeviceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<DeviceDto>>> GetMine(
        [FromServices] IQueryHandler<GetMyDevices, IReadOnlyList<DeviceDto>> handler,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await handler.HandleAsync(new GetMyDevices { UserId = userId }, cancellationToken));
    }

    /// <summary>
    /// Rejestruje token FCM (<c>platform</c>: Web | Android | Ios). Idempotentne: ten sam token jeszcze raz – to samo
    /// urządzenie z nowym <c>lastSeenAt</c>; token z urządzenia, na którym był zalogowany ktoś inny, przechodzi na mnie.
    /// Max 10 urządzeń – najdawniej widziane wypada. Zwraca urządzenie (z <c>id</c> do <c>DELETE</c>).
    /// </summary>
    [HttpPost]
    [ProducesResponseType<DeviceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DeviceDto>> Register(
        [FromBody] RegisterDevice command,
        [FromServices] ICommandHandler<RegisterDevice> handler,
        [FromServices] IQueryHandler<GetMyDeviceByToken, DeviceDto?> query,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await handler.HandleAsync(command with { DeviceId = Guid.NewGuid(), UserId = userId }, cancellationToken);

        var device = await query.HandleAsync(new GetMyDeviceByToken { UserId = userId, Token = command.Token },
            cancellationToken);

        return device is null ? NotFound() : Ok(device);
    }

    /// <summary>Usuwa urządzenie (koniec push na nim). Cudze albo nieistniejące → 404.</summary>
    [HttpDelete("{deviceId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(
        Guid deviceId,
        [FromServices] ICommandHandler<RemoveDevice> handler,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await handler.HandleAsync(new RemoveDevice(deviceId, userId), cancellationToken);
        return NoContent();
    }
}
