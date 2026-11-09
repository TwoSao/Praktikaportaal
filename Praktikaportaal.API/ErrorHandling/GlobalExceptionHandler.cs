using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.Core.Exceptions;

namespace Praktikaportaal.API.ErrorHandling;

/// <summary>
/// Converts every unhandled exception into an RFC 7807 ProblemDetails response.
/// Domain exceptions map to 400/401/403/404/409; anything else is logged and returned as 500
/// without leaking internal details. Controllers therefore contain no try/catch.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            NotFoundException e => (StatusCodes.Status404NotFound, e.Title, e.Message),
            ForbiddenException e => (StatusCodes.Status403Forbidden, e.Title, e.Message),
            UnauthorizedException e => (StatusCodes.Status401Unauthorized, e.Title, e.Message),
            ConflictException e => (StatusCodes.Status409Conflict, e.Title, e.Message),
            BusinessRuleException e => (StatusCodes.Status400BadRequest, e.Title, e.Message),
            BadHttpRequestException e => (e.StatusCode, "Bad request", e.Message),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error",
                  "An unexpected error occurred. Please try again later.")
        };

        if (status >= 500)
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        else
            _logger.LogInformation("Request {Method} {Path} failed with {Status}: {Message}",
                httpContext.Request.Method, httpContext.Request.Path, status, exception.Message);

        httpContext.Response.StatusCode = status;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Type = $"https://httpstatuses.io/{status}"
            }
        });
    }
}
