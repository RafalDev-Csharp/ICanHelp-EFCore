using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Commands;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Application.Security;
using ICanHelp.Core.Enums;
using ICanHelp.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ICanHelp.Api.Controllers;

/// <summary>Użytkownicy: rejestracja, logowanie, profil, oceny i polubienia.</summary>
[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    /// <summary>Rejestracja nowego konta (rola zawsze User). Zwraca adres utworzonego użytkownika.</summary>
    [HttpPost("sign-up")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] SignUp command,
        [FromServices] ICommandHandler<SignUp> handler,
        CancellationToken cancellationToken)
    {
        var userId = Guid.NewGuid();
        await handler.HandleAsync(command with { UserId = userId }, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { userId }, null);
    }

    /// <summary>
    /// Logowanie e-mailem i hasłem. Zwraca JWT do nagłówka <c>Authorization: Bearer ...</c> (ważny 1 h)
    /// i refresh token do <c>POST api/users/refresh</c> (ważny 30 dni).
    /// </summary>
    [HttpPost("sign-in")]
    [AllowAnonymous]
    [ProducesResponseType<JwtDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<JwtDto>> Login(
        [FromBody] SignIn command,
        [FromServices] ICommandHandler<SignIn> handler,
        [FromServices] ITokenStorage tokenStorage,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(command, cancellationToken);

        return Ok(tokenStorage.Get());
    }

    /// <summary>
    /// Nowa para tokenów za refresh token z logowania (bez hasła). Użyty token przestaje działać – zapisz nowy.
    /// 401 <c>InvalidRefreshToken</c> = trzeba zalogować się ponownie.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<JwtDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JwtDto>> Refresh(
        [FromBody] RefreshSession command,
        [FromServices] ICommandHandler<RefreshSession> handler,
        [FromServices] ITokenStorage tokenStorage,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(command, cancellationToken);

        return Ok(tokenStorage.Get());
    }

    /// <summary>
    /// Wylogowanie: unieważnia refresh token (token dostępu wygaśnie sam). Nie wymaga ważnego tokenu dostępu –
    /// wystarczy refresh token. Zawsze 204, także dla nieznanego tokenu.
    /// </summary>
    [HttpPost("sign-out")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] EndSession command,
        [FromServices] ICommandHandler<EndSession> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(command, cancellationToken);

        return NoContent();
    }

    /// <summary>Profil zalogowanego użytkownika (wymaga tokenu).</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> Me(
        [FromServices] IQueryHandler<GetUserById, UserDto?> handler,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(AuthClaims.UserId)?.Value, out var userId))
        {
            return Unauthorized();
        }

        var user = await handler.HandleAsync(new GetUserById { UserId = userId }, cancellationToken);

        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Lista wszystkich kont z pełnymi danymi – tylko dla admina.</summary>
    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll(
        [FromQuery] GetUsers query,
        [FromServices] IQueryHandler<GetUsers, IReadOnlyList<UserDto>> handler,
        CancellationToken cancellationToken)
        => Ok(await handler.HandleAsync(query, cancellationToken));

    /// <summary>Publiczny profil użytkownika (bez e-maila, telefonu i adresu).</summary>
    [HttpGet("{userId:guid}")]
    [ProducesResponseType<UserPublicDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserPublicDto>> GetById(
        Guid userId,
        [FromServices] IQueryHandler<GetUserPublicById, UserPublicDto?> handler,
        CancellationToken cancellationToken)
    {
        var user = await handler.HandleAsync(new GetUserPublicById { UserId = userId }, cancellationToken);

        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>
    /// F11 E: publiczne liczniki – ile zleceń użytkownik sam zerwał przed rozpoczęciem (<c>abandonedCount</c>)
    /// i przerwał w trakcie (<c>interruptedCount</c>). Rezygnacja drugiej strony się nie liczy.
    /// </summary>
    [HttpGet("{userId:guid}/reliability")]
    [ProducesResponseType<UserReliabilityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserReliabilityDto>> GetReliability(
        Guid userId,
        [FromServices] IQueryHandler<GetUserReliability, UserReliabilityDto?> handler,
        CancellationToken cancellationToken)
    {
        var reliability = await handler.HandleAsync(new GetUserReliability { UserId = userId }, cancellationToken);

        return reliability is null ? NotFound() : Ok(reliability);
    }

    /// <summary>Oceny, które użytkownik otrzymał – podstawa jego reputacji.</summary>
    [HttpGet("{userId:guid}/ratings")]
    [ProducesResponseType<IReadOnlyList<RatingDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RatingDto>>> GetRatings(
        Guid userId,
        [FromServices] IQueryHandler<GetRatingsByUser, IReadOnlyList<RatingDto>> handler,
        CancellationToken cancellationToken)
        => Ok(await handler.HandleAsync(new GetRatingsByUser { UserId = userId }, cancellationToken));

    /// <summary>Ogłoszenia polubione przez użytkownika – widzi je tylko on sam (albo admin).</summary>
    [HttpGet("{userId:guid}/liked-announcements")]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<LikedAnnouncementDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<LikedAnnouncementDto>>> GetLikedAnnouncements(
        Guid userId,
        [FromServices] IQueryHandler<GetLikedAnnouncements, IReadOnlyList<LikedAnnouncementDto>> handler,
        CancellationToken cancellationToken)
    {
        if (!IsSelfOrAdmin(userId))
        {
            return Forbid();
        }

        return Ok(await handler.HandleAsync(new GetLikedAnnouncements { UserId = userId }, cancellationToken));
    }

    /// <summary>Użytkownicy polubieni przez użytkownika – widzi ich tylko on sam (albo admin).</summary>
    [HttpGet("{userId:guid}/liked-users")]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<LikedUserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<LikedUserDto>>> GetLikedUsers(
        Guid userId,
        [FromServices] IQueryHandler<GetLikedUsers, IReadOnlyList<LikedUserDto>> handler,
        CancellationToken cancellationToken)
    {
        if (!IsSelfOrAdmin(userId))
        {
            return Forbid();
        }

        return Ok(await handler.HandleAsync(new GetLikedUsers { UserId = userId }, cancellationToken));
    }

    /// <summary>Czy zalogowany użytkownik to <paramref name="userId"/> albo admin.</summary>
    private bool IsSelfOrAdmin(Guid userId)
        => User.IsInRole(nameof(UserRole.Admin))
           || (Guid.TryParse(User.FindFirst(AuthClaims.UserId)?.Value, out var currentUserId) && currentUserId == userId);
}
