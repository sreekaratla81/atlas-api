using Atlas.DbMigrator;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Atlas.DbMigrator.Tests;

public class MigratorRunnerTests
{
    [Fact]
    public async Task RunAsync_Returns2_WhenCheckOnlyAndPendingMigrationsExist()
    {
        var executor = new FakeMigrationExecutor(["20240101010101_Init"]);
        var logger = LoggerFactory.Create(builder => { }).CreateLogger("test");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await MigratorApp.RunAsync(
            new MigratorOptions("Server=.;Database=Atlas;", true),
            executor,
            logger,
            output,
            error,
            "Server=.;Database=Atlas",
            CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Equal("20240101010101_Init" + Environment.NewLine, output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_Returns0_WhenApplySucceeds()
    {
        var executor = new FakeMigrationExecutor([]);
        var logger = LoggerFactory.Create(builder => { }).CreateLogger("test");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await MigratorApp.RunAsync(
            new MigratorOptions("Server=.;Database=Atlas;", false),
            executor,
            logger,
            output,
            error,
            "Server=.;Database=Atlas",
            CancellationToken.None);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_Returns1_WhenExecutorThrows()
    {
        var executor = new FakeMigrationExecutor([], throwsOnApply: true);
        var logger = LoggerFactory.Create(builder => { }).CreateLogger("test");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await MigratorApp.RunAsync(
            new MigratorOptions("Server=.;Database=Atlas;", false),
            executor,
            logger,
            output,
            error,
            "Server=.;Database=Atlas",
            CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Contains("Migration failed for Server=.;Database=Atlas", error.ToString());
    }

    private sealed class FakeMigrationExecutor(IReadOnlyList<string> pending, bool throwsOnApply = false) : IMigrationExecutor
    {
        private readonly IReadOnlyList<string> _pending = pending;
        private readonly bool _throwsOnApply = throwsOnApply;

        public Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_pending);
        }

        public Task ApplyMigrationsAsync(CancellationToken cancellationToken)
        {
            if (_throwsOnApply)
            {
                throw new InvalidOperationException("boom");
            }

            return Task.CompletedTask;
        }
    }
}
