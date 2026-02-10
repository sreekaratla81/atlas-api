using Atlas.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;
using System.Collections.Generic;
using System.Linq;

namespace Atlas.Api.Tests;

public class CorsConfigurationTests
{
    [Fact]
    public void BuildAllowedOrigins_IncludesRequiredAllowlist()
    {
        var env = new TestWebHostEnvironment { EnvironmentName = "Production" };
        var config = new ConfigurationBuilder().Build();

        var origins = Program.BuildAllowedOrigins(config, env);

        var requiredOrigins = new[]
        {
            "http://localhost:5173",
            "https://admin.atlashomestays.com",
            "https://dev.atlashomestays.com",
            "https://devadmin.atlashomestays.com",
            "https://www.atlashomestays.com",
            "https://*.pages.dev"
        };

        Assert.All(requiredOrigins, origin => Assert.Contains(origin, origins));
        Assert.DoesNotContain("http://127.0.0.1:5173", origins);
    }

    [Fact]
    public void BuildAllowedOrigins_AddsLoopbackInDevelopment()
    {
        var env = new TestWebHostEnvironment { EnvironmentName = "Development" };
        var config = new ConfigurationBuilder().Build();

        var origins = Program.BuildAllowedOrigins(config, env);

        Assert.Contains("http://127.0.0.1:5173", origins);
    }

    [Fact]
    public void BuildAllowedOrigins_MergesAdditionalOriginsDistinctly()
    {
        var env = new TestWebHostEnvironment { EnvironmentName = "Production" };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AdditionalOrigins:0"] = " https://extra.atlashomestays.com ",
                ["Cors:AdditionalOrigins:1"] = "https://admin.atlashomestays.com"
            })
            .Build();

        var origins = Program.BuildAllowedOrigins(config, env);

        Assert.Contains("https://extra.atlashomestays.com", origins);
        Assert.Equal(origins.Length, origins.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
    }
}
