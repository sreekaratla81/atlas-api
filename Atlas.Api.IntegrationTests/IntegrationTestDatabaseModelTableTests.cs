using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
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

        Assert.Contains(tables, table => table.Table == "EnvironmentMarker");
        Assert.Contains(tables, table => table.Table == "Properties");
        Assert.Contains(tables, table => table.Table == "ConsumedEvent");
        Assert.DoesNotContain(tables, table => table.Table == "__EFMigrationsHistory");
        Assert.All(tables, table => Assert.False(string.IsNullOrWhiteSpace(table.Schema)));
    }

    [Fact]
    public void BuildMissingTablesDiagnostic_IncludesExpectedDetails()
    {
        var message = IntegrationTestDatabase.BuildMissingTablesDiagnostic(
            "AtlasTestDb",
            new List<(string Schema, string Table)>
            {
                ("dbo", "EnvironmentMarkers"),
                ("dbo", "Properties")
            },
            new List<string> { "20250629080000_InitialBaseline" },
            new List<string> { "20250629080000_InitialBaseline" });

        Assert.Contains("AtlasTestDb", message);
        Assert.Contains("dbo.EnvironmentMarkers", message);
        Assert.Contains("dbo.Properties", message);
        Assert.Contains("20250629080000_InitialBaseline", message);
        Assert.Contains("Respawner initialization failed", message);
    }
}
