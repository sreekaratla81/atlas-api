using System;
using Xunit;

namespace Atlas.Api.IntegrationTests;

public class IntegrationDatabaseSafetyTests
{
    [Fact]
    public void Factory_Throws_WhenConnectionStringIsNotLocalDb()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            _ = new CustomWebApplicationFactory("Server=tcp:prod.example.net;Database=AtlasProd;User Id=sa;Password=bad;");
        });

        Assert.Contains("LocalDb", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
