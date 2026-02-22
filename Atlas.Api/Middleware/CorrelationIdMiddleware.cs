namespace Atlas.Api.Middleware;

/// <summary>
/// Reads or generates a correlation ID for each request.
/// Stored in HttpContext.TraceIdentifier and returned via X-Correlation-Id response header.
/// Enables end-to-end request tracing through logs and downstream services.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var incoming)
            && !string.IsNullOrWhiteSpace(incoming))
        {
            context.TraceIdentifier = incoming!;
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers.TryAdd(HeaderName, context.TraceIdentifier);
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
