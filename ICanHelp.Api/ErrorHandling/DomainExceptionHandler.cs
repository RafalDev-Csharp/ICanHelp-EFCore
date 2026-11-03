using ICanHelp.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ICanHelp.Api.ErrorHandling;

/// <summary>
/// Zamienia wyjątki domeny na odpowiedzi ProblemDetails:
/// <list type="bullet">
/// <item><see cref="OfferNotFoundException"/> → 404</item>
/// <item><see cref="DomainRuleException"/> (złamana reguła biznesowa) → 409</item>
/// <item>pozostałe <see cref="CustomExceptions"/> (błędne dane, np. e-mail) → 400</item>
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
            OfferNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            DomainRuleException => (StatusCodes.Status409Conflict, "Business rule violated"),
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
