using ICanHelp.Api.Security;
using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Commands;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ICanHelp.Api.Controllers;

/// <summary>
/// Ogłoszenia: odczyt (publiczny) oraz cały przepływ – dodanie, negocjacja ofert, realizacja, oceny (wymaga tokenu).
/// Kto wykonuje akcję, zawsze wynika z tokenu (claim <c>sub</c>), nie z body.
/// </summary>
[ApiController]
[Route("api/announcements")]
public sealed class AnnouncementsController : ControllerBase
{
    // ---------- odczyt ----------

    /// <summary>
    /// Strona ogłoszeń, od najnowszych. Każdy filtr jest opcjonalny.
    /// Z <c>latitude</c> + <c>longitude</c> (i opcjonalnie <c>radiusKm</c>, domyślnie 10, max 100): tylko ogłoszenia
    /// w promieniu, od najbliższych, z polem <c>distanceKm</c>.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AnnouncementDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<AnnouncementDto>>> GetAll(
        [FromQuery] GetAnnouncements query,
        [FromServices] IQueryHandler<GetAnnouncements, IReadOnlyList<AnnouncementDto>> handler,
        CancellationToken cancellationToken)
        => Ok(await handler.HandleAsync(query, cancellationToken));

    /// <summary>Ogłoszenie z całą negocjacją (oferty) i zdjęciami.</summary>
    [HttpGet("{announcementId:guid}")]
    [ProducesResponseType<AnnouncementDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnouncementDetailsDto>> GetById(
        Guid announcementId,
        [FromServices] IQueryHandler<GetAnnouncementById, AnnouncementDetailsDto?> handler,
        CancellationToken cancellationToken)
    {
        var announcement = await handler.HandleAsync(
            new GetAnnouncementById { AnnouncementId = announcementId }, cancellationToken);

        return announcement is null ? NotFound() : Ok(announcement);
    }

    /// <summary>Oceny wystawione po zakończeniu ogłoszenia (najwyżej po jednej od każdej strony).</summary>
    [HttpGet("{announcementId:guid}/ratings")]
    [ProducesResponseType<IReadOnlyList<RatingDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RatingDto>>> GetRatings(
        Guid announcementId,
        [FromServices] IQueryHandler<GetRatingByAnnouncementId, IReadOnlyList<RatingDto>> handler,
        CancellationToken cancellationToken)
        => Ok(await handler.HandleAsync(
            new GetRatingByAnnouncementId { AnnouncementId = announcementId }, cancellationToken));

    /// <summary>
    /// Kontakt do drugiej strony (właściciel widzi pomocnika, pomocnik właściciela) – dopiero po akceptacji oferty.
    /// Dla osób spoza transakcji i przed akceptacją: 404.
    /// </summary>
    [HttpGet("{announcementId:guid}/contact")]
    [Authorize]
    [ProducesResponseType<AnnouncementContactDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnouncementContactDto>> GetContact(
        Guid announcementId,
        [FromServices] IQueryHandler<GetAnnouncementContact, AnnouncementContactDto?> handler,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var contact = await handler.HandleAsync(
            new GetAnnouncementContact { AnnouncementId = announcementId, UserId = userId }, cancellationToken);

        return contact is null ? NotFound() : Ok(contact);
    }

    // ---------- ogłoszenie ----------

    /// <summary>Dodaje ogłoszenie; właścicielem jest zalogowany użytkownik. Zwraca id i adres ogłoszenia.</summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType<CreatedDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CreatedDto>> Create(
        [FromBody] CreateAnnouncement command,
        [FromServices] ICommandHandler<CreateAnnouncement> handler,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var announcementId = Guid.NewGuid();
        await handler.HandleAsync(command with { AnnouncementId = announcementId, OwnerId = userId }, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { announcementId }, new CreatedDto(announcementId));
    }

    /// <summary>Anuluje ogłoszenie (tylko właściciel, w statusie Open albo Accepted).</summary>
    [HttpPost("{announcementId:guid}/cancel")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Cancel(Guid announcementId,
        [FromServices] ICommandHandler<CancelAnnouncement> handler, CancellationToken cancellationToken)
        => Execute(userId => new CancelAnnouncement(announcementId, userId), handler, cancellationToken);

    /// <summary>Rozpoczęcie pracy (właściciel albo pomocnik z zaakceptowanej oferty).</summary>
    [HttpPost("{announcementId:guid}/start")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Start(Guid announcementId,
        [FromServices] ICommandHandler<StartAnnouncement> handler, CancellationToken cancellationToken)
        => Execute(userId => new StartAnnouncement(announcementId, userId), handler, cancellationToken);

    /// <summary>Potwierdzenie wykonania (tylko właściciel). Potem obie strony mogą się ocenić.</summary>
    [HttpPost("{announcementId:guid}/complete")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Complete(Guid announcementId,
        [FromServices] ICommandHandler<CompleteAnnouncement> handler, CancellationToken cancellationToken)
        => Execute(userId => new CompleteAnnouncement(announcementId, userId), handler, cancellationToken);

    /// <summary>Ocena drugiej strony po zakończeniu (raz na ogłoszenie; ocenianego wybiera domena).</summary>
    [HttpPost("{announcementId:guid}/ratings")]
    [Authorize]
    [ProducesResponseType<CreatedDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedDto>> PostRating(
        Guid announcementId,
        [FromBody] RateAnnouncement command,
        [FromServices] ICommandHandler<RateAnnouncement> handler,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var ratingId = Guid.NewGuid();
        await handler.HandleAsync(
            command with { AnnouncementId = announcementId, RatingId = ratingId, RaterId = userId }, cancellationToken);

        return CreatedAtAction(nameof(GetRatings), new { announcementId }, new CreatedDto(ratingId));
    }

    // ---------- oferty ----------

    /// <summary>Oferta pomocnika (zalogowany użytkownik, nie właściciel). Zwraca id oferty.</summary>
    [HttpPost("{announcementId:guid}/offers")]
    [Authorize]
    [ProducesResponseType<CreatedDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedDto>> PostOffer(
        Guid announcementId,
        [FromBody] MakeOffer command,
        [FromServices] ICommandHandler<MakeOffer> handler,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var offerId = Guid.NewGuid();
        await handler.HandleAsync(
            command with { AnnouncementId = announcementId, OfferId = offerId, HelperId = userId }, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { announcementId }, new CreatedDto(offerId));
    }

    /// <summary>Kontroferta do oczekującej oferty (składa ją druga strona niż autor). Zwraca id nowej oferty.</summary>
    [HttpPost("{announcementId:guid}/offers/{offerId:guid}/counter")]
    [Authorize]
    [ProducesResponseType<CreatedDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedDto>> PostCounterOffer(
        Guid announcementId,
        Guid offerId,
        [FromBody] CounterOffer command,
        [FromServices] ICommandHandler<CounterOffer> handler,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var newOfferId = Guid.NewGuid();
        await handler.HandleAsync(command with
        {
            AnnouncementId = announcementId,
            OfferId = offerId,
            NewOfferId = newOfferId,
            ActorId = userId
        }, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { announcementId }, new CreatedDto(newOfferId));
    }

    /// <summary>Akceptacja oferty (druga strona niż autor). Pozostałe oczekujące oferty zostają odrzucone.</summary>
    [HttpPost("{announcementId:guid}/offers/{offerId:guid}/accept")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Accept(Guid announcementId, Guid offerId,
        [FromServices] ICommandHandler<AcceptOffer> handler, CancellationToken cancellationToken)
        => Execute(userId => new AcceptOffer(announcementId, offerId, userId), handler, cancellationToken);

    /// <summary>Odrzucenie oferty (druga strona niż autor).</summary>
    [HttpPost("{announcementId:guid}/offers/{offerId:guid}/reject")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Reject(Guid announcementId, Guid offerId,
        [FromServices] ICommandHandler<RejectOffer> handler, CancellationToken cancellationToken)
        => Execute(userId => new RejectOffer(announcementId, offerId, userId), handler, cancellationToken);

    /// <summary>Wycofanie własnej oferty.</summary>
    [HttpPost("{announcementId:guid}/offers/{offerId:guid}/withdraw")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Withdraw(Guid announcementId, Guid offerId,
        [FromServices] ICommandHandler<WithdrawOffer> handler, CancellationToken cancellationToken)
        => Execute(userId => new WithdrawOffer(announcementId, offerId, userId), handler, cancellationToken);

    /// <summary>Akcje bez body: komenda z id z trasy + id z tokenu, odpowiedź 204.</summary>
    private async Task<IActionResult> Execute<TCommand>(Func<Guid, TCommand> createCommand,
        ICommandHandler<TCommand> handler, CancellationToken cancellationToken)
        where TCommand : class, ICommand
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await handler.HandleAsync(createCommand(userId), cancellationToken);
        return NoContent();
    }
}
