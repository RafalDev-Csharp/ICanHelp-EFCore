using ICanHelp.Application.Abstractions;
using ICanHelp.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace ICanHelp.Api.Controllers;

/// <summary>
/// Baza kontrolerów: kto wykonuje żądanie (id z tokenu) i dwie odpowiedzi, które powtarzają się w każdym kontrolerze –
/// 204 po komendzie i 404, gdy zapytanie niczego nie znalazło.
/// </summary>
[ApiController]
public abstract class ApiController : ControllerBase
{
    /// <summary>
    /// Id zalogowanego użytkownika (claim <c>sub</c>) – komendy biorą je stąd, nigdy z body. Tylko w akcjach
    /// z <c>[Authorize]</c>: token bez poprawnego id odrzuca już uwierzytelnianie (401), więc tutaj zawsze jest.
    /// </summary>
    protected Guid CurrentUserId
        => CurrentUserIdOrNull
           ?? throw new InvalidOperationException("The action reads the user id but does not require authentication.");

    /// <summary>Id zalogowanego użytkownika albo <c>null</c> – dla publicznych akcji, w których token jest opcjonalny.</summary>
    protected Guid? CurrentUserIdOrNull => User.TryGetUserId(out var userId) ? userId : null;

    /// <summary>Wykonuje komendę i odpowiada 204 (akcje, które niczego nie zwracają).</summary>
    protected async Task<IActionResult> Send<TCommand>(TCommand command, ICommandHandler<TCommand> handler,
        CancellationToken cancellationToken)
        where TCommand : class, ICommand
    {
        await handler.HandleAsync(command, cancellationToken);
        return NoContent();
    }

    /// <summary>Wynik zapytania: 200 z danymi albo 404, gdy handler zwrócił <c>null</c> (nie ma / cudzy zasób).</summary>
    protected ActionResult<T> OkOrNotFound<T>(T? result) where T : class
        => result is null ? NotFound() : Ok(result);
}
