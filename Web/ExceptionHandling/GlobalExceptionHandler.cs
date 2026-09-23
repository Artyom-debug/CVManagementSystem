using Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Web.ExceptionHandling;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ValidationException validationException)
        {
            var errors = validationException.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(error => error.ErrorMessage)
                        .Distinct()
                        .ToArray());

            var validationProblem = new HttpValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed."
            };

            validationProblem.Extensions["traceId"] = httpContext.TraceIdentifier;
            httpContext.Response.StatusCode = validationProblem.Status.Value;
            await httpContext.Response.WriteAsJsonAsync(validationProblem, cancellationToken);
            return true;
        }

        var (statusCode, title, detail) = exception switch
        {
            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized.",
                exception.Message),

            ForbiddenAccessException => (
                StatusCodes.Status403Forbidden,
                "Access denied.",
                exception.Message),

            NotFoundException => (
                StatusCodes.Status404NotFound,
                "Resource not found.",
                exception.Message),

            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "Concurrency conflict.",
                "The resource was changed by another request. Reload it and try again."),

            ArgumentException => (
                StatusCodes.Status400BadRequest,
                "Invalid request.",
                exception.Message),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal server error.",
                "An unexpected error occurred.")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "An unhandled exception occurred while processing the request.");
        else
            _logger.LogWarning(exception, "Request failed with status code {StatusCode}.", statusCode);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
