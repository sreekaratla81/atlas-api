using Atlas.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace Atlas.Api.Tests;

public class MigrationGuardTests
{
    [Theory]
    [InlineData("Development", false, true)]
    [InlineData("Test", false, true)]
    [InlineData("Production", false, false)]
    [InlineData("Production", true, true)]
    public void ShouldRunMigrations_ReturnsExpectedResult(string environmentName, bool runMigrations, bool expected)
    {
        var env = new TestWebHostEnvironment { EnvironmentName = environmentName };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RunMigrations"] = runMigrations.ToString()
            })
            .Build();

        var result = Program.ShouldRunMigrations(env, config);

        Assert.Equal(expected, result);
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = string.Empty;
        public string ApplicationName { get; set; } = "Atlas.Api";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
