using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantHandler.Infrastructure.Errors;

/// <summary>
/// Gestionnaire global : une erreur métier devient un <c>ProblemDetails</c> portant son message,
/// toute autre exception un 500 sans détail, journalisé. La trace n'est exposée qu'en
/// développement.
/// </summary>
public sealed class BusinessExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<BusinessExceptionHandler> logger
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        var (status, detail) = exception switch
        {
            BusinessException business => (business.StatusCode, business.Message),
            InvalidReservationTransitionException => (StatusCodes.Status409Conflict, exception.Message),
            _ => (StatusCodes.Status500InternalServerError, null),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Erreur non gérée sur {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path
            );
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = status == StatusCodes.Status500InternalServerError
                ? "Une erreur inattendue est survenue"
                : detail,
            Detail = detail,
        };
        // Le front lit « message » dans l'enveloppe Response<T> : on le garde le temps du lot 7.
        problem.Extensions["message"] = problem.Title;
        if (status == StatusCodes.Status500InternalServerError && environment.IsDevelopment())
        {
            problem.Extensions["exception"] = exception.ToString();
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = problem,
            }
        );
    }
}
