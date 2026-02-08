using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Atlas.Api.Tests.Data;

public class DatabaseSchemaInitializerTests
{
    [Fact]
    public async Task EnsureSchemaAsync_UsesEnsureCreated_WhenNoMigrationsExist()
    {
        var dbName = $"AtlasSchema_NoMigrations_{Guid.NewGuid():N}";
        var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;";

        var migrationsAssembly = typeof(DatabaseSchemaInitializerTests).Assembly.GetName().Name;
        var options = new DbContextOptionsBuilder<NoMigrationsDbContext>()
            .UseSqlServer(connectionString, sqlOptions =>
                sqlOptions.MigrationsAssembly(migrationsAssembly))
            .Options;

        await using var db = new NoMigrationsDbContext(options);
        try
        {
            var usedEnsureCreated = await DatabaseSchemaInitializer.EnsureSchemaAsync(db.Database);

            Assert.True(usedEnsureCreated);
            var entities = await db.NoMigrationEntities.ToListAsync();
            Assert.NotNull(entities);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task EnsureSchemaAsync_UsesMigrations_WhenMigrationsExist()
    {
        var dbName = $"AtlasSchema_WithMigrations_{Guid.NewGuid():N}";
        var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;";

        var migrationsAssembly = typeof(AppDbContext).Assembly.GetName().Name;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sqlOptions =>
                sqlOptions.MigrationsAssembly(migrationsAssembly))
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

    [Fact]
    public async Task EnsureSchemaAsync_UsesMigrations_WhenMigrationsAssemblyIsMismatched()
    {
        var dbName = $"AtlasSchema_Fallback_{Guid.NewGuid():N}";
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

            Assert.False(usedEnsureCreated);
            var markers = await db.EnvironmentMarkers.ToListAsync();
            Assert.NotNull(markers);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    private sealed class NoMigrationsDbContext : DbContext
    {
        public NoMigrationsDbContext(DbContextOptions<NoMigrationsDbContext> options)
            : base(options)
        {
        }

        public DbSet<NoMigrationEntity> NoMigrationEntities => Set<NoMigrationEntity>();
    }

    private sealed class NoMigrationEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }
}
