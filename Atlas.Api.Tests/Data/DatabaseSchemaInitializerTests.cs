using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Atlas.Api.Tests.Data;

public class DatabaseSchemaInitializerTests
{
    [Fact]
    public async Task EnsureSchemaAsync_UsesEnsureCreated_WhenNoMigrationsExist()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var dbName = $"AtlasSchema_NoMigrations_{Guid.NewGuid():N}";
        var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;";

        var migrationsAssembly = typeof(DatabaseSchemaInitializerTests).Assembly.GetName().Name;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sqlOptions =>
                sqlOptions.MigrationsAssembly(migrationsAssembly))
            .Options;

        await using var db = new AppDbContext(options);
        try
        {
            var usedEnsureCreated = await DatabaseSchemaInitializer.EnsureSchemaAsync(db.Database);

            Assert.True(usedEnsureCreated);
            Assert.Empty(db.Database.GetService<IMigrationsAssembly>().Migrations);
            var markers = await db.EnvironmentMarkers.ToListAsync();
            Assert.NotNull(markers);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

}
