using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Atlas.Api.IntegrationTests;

public class IntegrationTestDatabaseModelTableTests
{
    [Fact]
    public void GetModelTableNames_ExcludesMigrationsHistoryAndIncludesExpectedTables()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=AtlasUnitTest_ModelTables;Trusted_Connection=True;")
            .Options;

        using var db = new AppDbContext(options);

        var tables = IntegrationTestDatabase.GetModelTableNames(db);

        Assert.Contains(tables, table => table.Table == "EnvironmentMarkers");
        Assert.Contains(tables, table => table.Table == "Properties");
        Assert.DoesNotContain(tables, table => table.Table == "__EFMigrationsHistory");
        Assert.All(tables, table => Assert.False(string.IsNullOrWhiteSpace(table.Schema)));
    }
}
