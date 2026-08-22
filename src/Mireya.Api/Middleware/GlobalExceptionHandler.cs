using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Mireya.Api.Middleware;

/// <summary>
///     Translates uncaught exceptions into RFC 7807 ProblemDetails responses so the API
///     returns a consistent error contract instead of leaking stack traces or returning bare 500s.
/// </summary>
public class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        var (status, title) = exception switch
        {
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            InvalidOperationException => (
                StatusCodes.Status409Conflict,
                "Operation could not be completed"
            ),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Access denied"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred"),
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(
                exception,
                "Unhandled exception processing {Path}",
                httpContext.Request.Path
            );
        else
            logger.LogWarning(
                exception,
                "Request to {Path} failed: {Message}",
                httpContext.Request.Path,
                exception.Message
            );

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ProblemDetails
                {
                    Status = status,
                    Title = title,
                    // Only surface the message for client errors; hide internals on 500.
                    Detail = status >= 500 ? null : exception.Message,
                },
            }
        );
    }
}
