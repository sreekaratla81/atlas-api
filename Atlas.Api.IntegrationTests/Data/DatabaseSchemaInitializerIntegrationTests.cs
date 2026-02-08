using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Atlas.Api.IntegrationTests.Data;

public class DatabaseSchemaInitializerIntegrationTests
{
    [Fact]
    public async Task EnsureSchemaAsync_AppliesMigrations_WhenPresent()
    {
        var dbName = $"AtlasSchema_Integration_{Guid.NewGuid():N}";
        var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sqlOptions =>
                sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        await using var db = new AppDbContext(options);
        try
        {
            var usedEnsureCreated = await DatabaseSchemaInitializer.EnsureSchemaAsync(db.Database);

            Assert.False(usedEnsureCreated);
            var markers = await db.EnvironmentMarkers.ToListAsync();
            Assert.NotNull(markers);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
