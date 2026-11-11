using Microsoft.AspNetCore.Mvc;
using Dixon.LabelVerification.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Dixon.LabelVerification.API.Middleware;

public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path} traceId={TraceId}",
                context.Request.Method, context.Request.Path, context.TraceIdentifier);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            var (statusCode, title) = exception switch
            {
                InspectionNotFoundException =>
                    (StatusCodes.Status404NotFound, exception.Message),
                InspectionConflictException =>
                    (StatusCodes.Status409Conflict, exception.Message),
                InspectionValidationException =>
                    (StatusCodes.Status400BadRequest, exception.Message),
                InspectionProcessingException =>
                    (StatusCodes.Status502BadGateway, exception.Message),
                DbUpdateConcurrencyException =>
                    (StatusCodes.Status409Conflict, "The inspection changed while this request was in progress. Refresh and retry."),
                UnauthorizedAccessException =>
                    (StatusCodes.Status401Unauthorized, exception.Message),
                ConfigurationNotFoundException =>
                    (StatusCodes.Status404NotFound, exception.Message),
                ConfigurationConflictException =>
                    (StatusCodes.Status409Conflict, exception.Message),
                DbUpdateException =>
                    (StatusCodes.Status409Conflict, "The change conflicts with existing configuration or references."),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected server error occurred.")
            };
            if (statusCode != StatusCodes.Status500InternalServerError)
            {
                logger.LogWarning(exception, "Configuration request rejected with status {StatusCode}", statusCode);
            }

            context.Response.StatusCode = statusCode;
            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Instance = context.Request.Path
            };
            problem.Extensions["traceId"] = context.TraceIdentifier;
            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
