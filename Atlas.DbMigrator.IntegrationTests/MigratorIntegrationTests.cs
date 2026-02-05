using Atlas.Api.Data;
using Atlas.DbMigrator;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Atlas.DbMigrator.IntegrationTests;

public class MigratorIntegrationTests
{
    [Fact]
    public async Task AppliesMigrationsAgainstLocalDb()
    {
        var connectionString = BuildLocalDbConnectionString();
        var args = new[] { "--connection", connectionString };

        try
        {
            var exitCode = await MigratorApp.RunAsync(args, TextWriter.Null, TextWriter.Null, CancellationToken.None);

            Assert.Equal(0, exitCode);

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            await using var dbContext = new AppDbContext(options);
            var pending = await dbContext.Database.GetPendingMigrationsAsync();

            Assert.Empty(pending);
        }
        finally
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            await using var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureDeletedAsync();
        }
    }

    private static string BuildLocalDbConnectionString()
    {
        var testRunId = Environment.GetEnvironmentVariable("ATLAS_TEST_RUN_ID")
            ?? DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        var databaseName = $"AtlasHomestays_TestDb_{testRunId}";
        return $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True";
    }
}
