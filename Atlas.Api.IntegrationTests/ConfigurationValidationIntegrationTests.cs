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
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = "__SET_VIA_ENV_OR_AZURE__",
                        ["Jwt:Key"] = "__SET_VIA_ENV_OR_AZURE__"
                    });
                });
            });

        var exception = Assert.ThrowsAny<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("DefaultConnection", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
