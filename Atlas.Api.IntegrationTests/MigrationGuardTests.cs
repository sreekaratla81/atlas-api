using Atlas.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Atlas.Api.IntegrationTests;

public class MigrationGuardTests
{
    [Fact]
    public void ShouldRunMigrations_AllowsProductionWhenEnabled()
    {
        var env = new TestWebHostEnvironment { EnvironmentName = Environments.Production };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RunMigrations"] = "true"
            })
            .Build();

        var result = Program.ShouldRunMigrations(env, config);

        Assert.True(result);
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
