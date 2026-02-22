using Atlas.Api.Middleware;
using Microsoft.AspNetCore.Http;

namespace Atlas.Api.Tests;

public class CorrelationIdMiddlewareTests
{
    private const string HeaderName = "X-Correlation-Id";

    private static RequestDelegate StartResponseDelegate =>
        ctx => ctx.Response.StartAsync();

    [Fact]
    public async Task EchoesProvidedCorrelationId()
    {
        var expected = Guid.NewGuid().ToString();
        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderName] = expected;

        var middleware = new CorrelationIdMiddleware(StartResponseDelegate);
        await middleware.InvokeAsync(context);

        Assert.Equal(expected, context.Response.Headers[HeaderName].ToString());
    }

    [Fact]
    public async Task GeneratesCorrelationIdWhenNoneProvided()
    {
        var context = new DefaultHttpContext();

        var middleware = new CorrelationIdMiddleware(StartResponseDelegate);
        await middleware.InvokeAsync(context);

        Assert.False(string.IsNullOrWhiteSpace(context.Response.Headers[HeaderName].ToString()));
    }

    [Fact]
    public async Task SetsTraceIdentifierFromHeader()
    {
        var expected = "my-trace-id-123";
        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderName] = expected;

        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);

        Assert.Equal(expected, context.TraceIdentifier);
    }
}
