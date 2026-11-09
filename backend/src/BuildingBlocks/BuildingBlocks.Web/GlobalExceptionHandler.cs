using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Web;

/// <summary>
/// Translates an exception the platform doesn't know about into ProblemDetails (e.g. a provider-specific
/// unique-constraint violation → 409). Registered by the app; the first mapper that returns a value wins.
/// Keeps exception translation in one place instead of try/catch blocks in handlers.
/// </summary>
public interface IExceptionProblemMapper
{
    ProblemDetails? TryMap(Exception exception);
}

/// <summary>
/// Converts unhandled exceptions into RFC 7807 ProblemDetails. A thrown
/// <see cref="ValidationException"/> (from the mediator validation behavior) becomes 400 with per-field
/// errors keyed by camelCase property path; registered <see cref="IExceptionProblemMapper"/>s get a chance
/// next; everything else is a 500 that never leaks internals. The trace id is always attached.
/// </summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IEnumerable<IExceptionProblemMapper> mappers,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            ValidationException validationException => FromValidation(validationException),
            // Minimal-API binding failures (malformed JSON, wrong types) surface as BadHttpRequestException in
            // Development; they are client errors, never 500s.
            BadHttpRequestException badRequest => new ProblemDetails { Status = badRequest.StatusCode, Title = "The request could not be read" },
            _ => mappers.Select(m => m.TryMap(exception)).FirstOrDefault(p => p is not null),
        };

        if (problemDetails is null)
        {
            logger.LogError(exception, "Unhandled exception. TraceId={TraceId}", httpContext.TraceIdentifier);
            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred",
            };
        }
        else
        {
            logger.LogInformation("Request rejected with {Status}: {Title}", problemDetails.Status, problemDetails.Title);
        }

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });
    }

    private static ProblemDetails FromValidation(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => PropertyPath.ToCamelCase(e.PropertyName), e => e.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.Distinct().ToArray());

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
        };
    }
}

/// <summary>Converts a FluentValidation property path ("Allowances[0].Amount") to the JSON path a client sent ("allowances[0].amount").</summary>
public static class PropertyPath
{
    public static string ToCamelCase(string path) =>
        string.Join('.', path.Split('.').Select(segment =>
            segment.Length == 0 ? segment : char.ToLowerInvariant(segment[0]) + segment[1..]));
}
