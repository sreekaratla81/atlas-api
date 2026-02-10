using Atlas.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using System;
using System.Collections.Generic;
using Xunit;

namespace Atlas.Api.Tests;

public class ConfigurationValidationTests
{
    [Fact]
    public void ValidateRequiredConfiguration_ThrowsWhenConnectionStringPlaceholderInProduction()
    {
        var env = new TestWebHostEnvironment { EnvironmentName = "Production" };
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "__SET_VIA_ENV_OR_AZURE__",
            ["Jwt:Key"] = "RealKeyValue"
        });

        var exception = Assert.Throws<InvalidOperationException>(() => Program.ValidateRequiredConfiguration(config, env));

        Assert.Contains("DefaultConnection", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRequiredConfiguration_ThrowsWhenJwtPlaceholderInProduction()
    {
        var env = new TestWebHostEnvironment { EnvironmentName = "Production" };
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=AtlasTest;Trusted_Connection=True;",
            ["Jwt:Key"] = "__SET_VIA_ENV_OR_AZURE__"
        });

        var exception = Assert.Throws<InvalidOperationException>(() => Program.ValidateRequiredConfiguration(config, env));

        Assert.Contains("Jwt:Key", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRequiredConfiguration_SkipsValidationInDevelopment()
    {
        var env = new TestWebHostEnvironment { EnvironmentName = "Development" };
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "__SET_VIA_ENV_OR_AZURE__",
            ["Jwt:Key"] = "__SET_VIA_ENV_OR_AZURE__"
        });

        var exception = Record.Exception(() => Program.ValidateRequiredConfiguration(config, env));

        Assert.Null(exception);
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

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
