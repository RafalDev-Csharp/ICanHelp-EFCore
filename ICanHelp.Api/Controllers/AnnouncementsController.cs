using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using Microsoft.AspNetCore.Mvc;

namespace ICanHelp.Api.Controllers;

/// <summary>Ogłoszenia (na razie tylko odczyt – komendy w Kroku 6).</summary>
[ApiController]
[Route("api/announcements")]
public sealed class AnnouncementsController : ControllerBase
{
    /// <summary>Strona ogłoszeń, od najnowszych. Każdy filtr jest opcjonalny.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AnnouncementDto>>(StatusCodes.Status200OK)]
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
}
