using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ICanHelp.Api.Controllers;

/// <summary>
/// Oceny zalogowanego (ekran „Oceny” w Koncie, F11 F): otrzymane – z rolą, w jakiej go oceniono – i „do wystawienia”
/// (zakończone zlecenia bez jego oceny). Wystawienie oceny: <c>POST api/announcements/{id}/ratings</c>.
/// </summary>
[Route("api/me/ratings")]
[Authorize]
public sealed class MyRatingsController : ApiController
{
    /// <summary>Otrzymane oceny od najnowszych; <c>myRole</c> = Client (jako zleceniodawca) albo Helper (jako pomocnik).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MyRatingDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<MyRatingDto>>> GetMine(
        [FromServices] IQueryHandler<GetMyRatings, IReadOnlyList<MyRatingDto>> handler,
        CancellationToken cancellationToken)
        => Ok(await handler.HandleAsync(new GetMyRatings { UserId = CurrentUserId }, cancellationToken));

    /// <summary>Zakończone zlecenia (jako właściciel albo przyjęty pomocnik), w których nie oceniłem drugiej strony.</summary>
    [HttpGet("pending")]
    [ProducesResponseType<IReadOnlyList<PendingRatingDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<PendingRatingDto>>> GetPending(
        [FromServices] IQueryHandler<GetMyPendingRatings, IReadOnlyList<PendingRatingDto>> handler,
        CancellationToken cancellationToken)
        => Ok(await handler.HandleAsync(new GetMyPendingRatings { UserId = CurrentUserId }, cancellationToken));
}
