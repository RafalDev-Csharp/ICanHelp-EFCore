using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using Microsoft.AspNetCore.Mvc;

namespace ICanHelp.Api.Controllers;

/// <summary>Użytkownicy, ich oceny i polubienia (rejestracja/logowanie w Kroku 5).</summary>
[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll(
        [FromQuery] GetUsers query,
        [FromServices] IQueryHandler<GetUsers, IReadOnlyList<UserDto>> handler,
        CancellationToken cancellationToken)
        => Ok(await handler.HandleAsync(query, cancellationToken));

    [HttpGet("{userId:guid}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetById(
        Guid userId,
        [FromServices] IQueryHandler<GetUserById, UserDto?> handler,
        CancellationToken cancellationToken)
    {
        var user = await handler.HandleAsync(new GetUserById { UserId = userId }, cancellationToken);

        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Oceny, które użytkownik otrzymał – podstawa jego reputacji.</summary>
    [HttpGet("{userId:guid}/ratings")]
    [ProducesResponseType<IReadOnlyList<RatingDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RatingDto>>> GetRatings(
        Guid userId,
        [FromServices] IQueryHandler<GetRatingsByUser, IReadOnlyList<RatingDto>> handler,
        CancellationToken cancellationToken)
        => Ok(await handler.HandleAsync(new GetRatingsByUser { UserId = userId }, cancellationToken));

    /// <summary>Ogłoszenia polubione przez użytkownika.</summary>
    [HttpGet("{userId:guid}/liked-announcements")]
    [ProducesResponseType<IReadOnlyList<LikedAnnouncementDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LikedAnnouncementDto>>> GetLikedAnnouncements(
        Guid userId,
        [FromServices] IQueryHandler<GetLikedAnnouncements, IReadOnlyList<LikedAnnouncementDto>> handler,
        CancellationToken cancellationToken)
        => Ok(await handler.HandleAsync(new GetLikedAnnouncements { UserId = userId }, cancellationToken));

    /// <summary>Użytkownicy polubieni przez użytkownika.</summary>
    [HttpGet("{userId:guid}/liked-users")]
    [ProducesResponseType<IReadOnlyList<LikedUserDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LikedUserDto>>> GetLikedUsers(
        Guid userId,
        [FromServices] IQueryHandler<GetLikedUsers, IReadOnlyList<LikedUserDto>> handler,
        CancellationToken cancellationToken)
        => Ok(await handler.HandleAsync(new GetLikedUsers { UserId = userId }, cancellationToken));
}
