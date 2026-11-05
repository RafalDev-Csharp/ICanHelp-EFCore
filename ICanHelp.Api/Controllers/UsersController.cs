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

    /// <summary>Logowanie e-mailem i hasłem. Zwraca JWT do nagłówka <c>Authorization: Bearer ...</c>.</summary>
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
