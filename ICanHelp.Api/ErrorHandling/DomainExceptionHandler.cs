using ICanHelp.Application.Exceptions;
using ICanHelp.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ICanHelp.Api.ErrorHandling;

/// <summary>
/// Zamienia wyjątki domeny na odpowiedzi ProblemDetails:
/// <list type="bullet">
/// <item><see cref="OfferNotFoundException"/>, <see cref="AnnouncementNotFoundException"/>, <see cref="UserNotFoundException"/>,
/// <see cref="ImageNotFoundException"/>, <see cref="ConversationNotFoundException"/> → 404</item>
/// <item><see cref="DomainRuleException"/> (złamana reguła biznesowa), zajęta nazwa/e-mail, ponowna ocena,
/// <see cref="ConcurrencyConflictException"/> (równoczesna zmiana) → 409</item>
/// <item><see cref="InvalidCredentialsException"/> → 401, <see cref="UserLockedException"/> → 403</item>
/// <item>pozostałe <see cref="CustomExceptions"/> (błędne dane, np. e-mail, <see cref="InvalidImageException"/>) → 400</item>
/// </list>
/// Inne wyjątki nie są tu obsługiwane – middleware zwróci 500 bez szczegółów.
/// </summary>
internal sealed class DomainExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<DomainExceptionHandler> _logger;

    public DomainExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<DomainExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not CustomExceptions domainException)
        {
            return false;
        }

        var (status, title) = domainException switch
        {
            OfferNotFoundException or AnnouncementNotFoundException or UserNotFoundException or ImageNotFoundException
                or ConversationNotFoundException
                => (StatusCodes.Status404NotFound, "Resource not found"),
            DomainRuleException => (StatusCodes.Status409Conflict, "Business rule violated"),
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, "Concurrent change"),
            UserNameAlreadyInUseException or EmailAlreadyInUseException or AlreadyRatedException
                => (StatusCodes.Status409Conflict, "Resource already exists"),
            InvalidCredentialsException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            UserLockedException => (StatusCodes.Status403Forbidden, "Forbidden"),
            _ => (StatusCodes.Status400BadRequest, "Invalid data")
        };

        _logger.LogInformation("Domain exception {ExceptionType} mapped to {StatusCode}: {Message}",
            domainException.GetType().Name, status, domainException.Message);

        httpContext.Response.StatusCode = status;

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = domainException.Message
        };
        // Nazwa wyjątku bez sufiksu, np. "DomainRule", "InvalidEmail" – klient może po niej rozpoznać błąd.
        problemDetails.Extensions["errorCode"] = domainException.GetType().Name.Replace("Exception", string.Empty);

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }
}
