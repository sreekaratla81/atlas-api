using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Filters;

/// <summary>
/// Global exception filter that converts common exceptions to consistent JSON error responses.
/// Registered once in Program.cs — provides baseline error handling for all controllers
/// without requiring per-action try-catch.
/// </summary>
public sealed class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;
    private readonly IHostEnvironment _env;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.ExceptionHandled) return;

        var (statusCode, message) = context.Exception switch
        {
            ArgumentException ex => (400, ex.Message),
            KeyNotFoundException ex => (404, ex.Message),
            InvalidOperationException ex when ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) => (404, ex.Message),
            UnauthorizedAccessException ex => (401, ex.Message),
            DbUpdateConcurrencyException => (409, "The record was modified by another request. Please retry."),
            DbUpdateException dbEx when IsUniqueConstraintViolation(dbEx) => (409, "A record with the same key already exists."),
            DbUpdateException dbEx when IsForeignKeyViolation(dbEx) => (409, "Cannot delete this record because it has related data. Remove dependent records first."),
            DbUpdateException => (422, "A database validation error occurred."),
            _ => (0, (string?)null)
        };

        if (statusCode == 0) return;

        _logger.LogWarning(context.Exception, "ApiExceptionFilter handled {StatusCode} for {Path}.",
            statusCode, context.HttpContext.Request.Path);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                400 => "Bad Request",
                401 => "Unauthorized",
                404 => "Not Found",
                409 => "Conflict",
                422 => "Validation Error",
                _ => "Error"
            },
            Detail = message,
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        context.Result = new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
        context.ExceptionHandled = true;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("2627", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("2601", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsForeignKeyViolation(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("REFERENCE constraint", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("547", StringComparison.OrdinalIgnoreCase);
    }
}
