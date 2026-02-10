using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Api.IntegrationTests;

public class MigrationSafetyTests : IntegrationTestBase
{
    public MigrationSafetyTests(SqlServerTestDatabase database) : base(database)
    {
    }

    [Fact]
    public void PendingMigrations_AreAppliedByTestHarness()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pendingMigrations = db.Database.GetPendingMigrations();

        Assert.Empty(pendingMigrations);
    }
}
