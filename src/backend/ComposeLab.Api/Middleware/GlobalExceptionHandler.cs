using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ComposeLab.Api.Middleware;

/// <summary>
/// Turns unexpected exceptions into RFC 7807 responses. Expected failures never arrive here — they
/// travel through <c>Result</c> instead, and the two paths stay separate.
/// </summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Unhandled exception handling {Method} {Path}.",
            httpContext.Request.Method,
            httpContext.Request.Path);

        if (exception is ValidationException validationException)
        {
            Dictionary<string, string[]> errors = validationException.Errors
                .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(failure => failure.ErrorMessage).ToArray(),
                    StringComparer.Ordinal);

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ValidationProblemDetails(errors)
                {
                    Status = StatusCodes.Status400BadRequest
                }
            });
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        // Deliberately generic. Stack traces, provider messages, and configuration values never
        // reach a client.
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Title = "An unexpected error occurred.",
                Status = StatusCodes.Status500InternalServerError
            }
        });
    }
}
