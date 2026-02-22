using Atlas.Api.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Atlas.Api.Tests;

public class ApiExceptionFilterTests
{
    private static ExceptionContext CreateContext(Exception exception)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor());

        return new ExceptionContext(actionContext, [])
        {
            Exception = exception
        };
    }

    private static ApiExceptionFilter CreateFilter()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Production");
        return new ApiExceptionFilter(
            NullLogger<ApiExceptionFilter>.Instance,
            env.Object);
    }

    [Fact]
    public void Returns400_ForArgumentException()
    {
        var filter = CreateFilter();
        var context = CreateContext(new ArgumentException("Bad value"));

        filter.OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public void Returns404_ForKeyNotFoundException()
    {
        var filter = CreateFilter();
        var context = CreateContext(new KeyNotFoundException("Not found"));

        filter.OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public void Returns409_ForDbUpdateConcurrencyException()
    {
        var filter = CreateFilter();
        var context = CreateContext(new DbUpdateConcurrencyException("Concurrency conflict"));

        filter.OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public void Returns401_ForUnauthorizedAccessException()
    {
        var filter = CreateFilter();
        var context = CreateContext(new UnauthorizedAccessException("Nope"));

        filter.OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public void DoesNotHandle_UnknownExceptions()
    {
        var filter = CreateFilter();
        var context = CreateContext(new InvalidOperationException("Something went wrong"));

        filter.OnException(context);

        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }
}
