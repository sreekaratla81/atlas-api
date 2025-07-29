using Atlas.Api.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Atlas.Api.IntegrationTests;

public class FallbackDbTests
{
    [Fact]
    public void UsesInMemory_WhenSqlServerUnavailable()
    {
        var original = Environment.GetEnvironmentVariable("Atlas_TestDb");
        Environment.SetEnvironmentVariable("Atlas_TestDb", "Server=invalid;Database=NoDb;User Id=foo;Password=bar;");
        try
        {
            using var factory = new CustomWebApplicationFactory();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(db.Database.IsInMemory());
        }
        finally
        {
            Environment.SetEnvironmentVariable("Atlas_TestDb", original);
        }
    }
}
