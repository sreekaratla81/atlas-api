using Atlas.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using Xunit;

namespace Atlas.Api.IntegrationTests;

public class ConfigurationValidationIntegrationTests
{
    [Fact]
    public void AppStartup_ThrowsWhenRequiredConfigurationMissingInProduction()
    {
        // Save and clear any existing environment variables that might interfere
        var originalConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        var originalJwtKey = Environment.GetEnvironmentVariable("Jwt__Key");
        try
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
            Environment.SetEnvironmentVariable("Jwt__Key", null);

            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Production");
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.Sources.Clear();
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:DefaultConnection"] = "__SET_VIA_ENV_OR_AZURE__",
                            ["Jwt:Key"] = "__SET_VIA_ENV_OR_AZURE__",
                            ["Startup:StrictRequiredConfig"] = "true"
                        });
                    });
                });

            var exception = Assert.ThrowsAny<InvalidOperationException>(() => factory.CreateClient());

            // Either connection string or JWT validation should fail (both are set to placeholder)
            var containsConnectionString = exception.Message.Contains("DefaultConnection", StringComparison.OrdinalIgnoreCase);
            var containsJwtKey = exception.Message.Contains("Jwt", StringComparison.OrdinalIgnoreCase);
            Assert.True(containsConnectionString || containsJwtKey, 
                $"Expected validation error for either DefaultConnection or JWT, but got: {exception.Message}");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", originalConnectionString);
            Environment.SetEnvironmentVariable("Jwt__Key", originalJwtKey);
        }
    }
}
