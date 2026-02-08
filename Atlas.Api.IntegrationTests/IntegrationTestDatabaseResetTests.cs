using Atlas.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using Xunit;

namespace Atlas.Api.IntegrationTests;

[Collection("IntegrationTests")]
public class IntegrationTestDatabaseResetTests : IClassFixture<SqlServerTestDatabase>, IAsyncLifetime
{
    private readonly SqlServerTestDatabase _database;
    private CustomWebApplicationFactory _factory = null!;
    private string? _previousDefaultConnection;

    public IntegrationTestDatabaseResetTests(SqlServerTestDatabase database)
    {
        _database = database;
    }

    public Task InitializeAsync()
    {
        _previousDefaultConnection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);

        _factory = new CustomWebApplicationFactory(_database.ConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _previousDefaultConnection);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ResetAsync_RecreatesRespawner_WhenDatabaseIsEmpty()
    {
        using var scope = _factory.Services.CreateScope();
        var connectionString = scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Database.GetDbConnection().ConnectionString;
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        var masterBuilder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        };

        await using (var masterConnection = new SqlConnection(masterBuilder.ConnectionString))
        {
            await masterConnection.OpenAsync();
            await using var command = masterConnection.CreateCommand();
            command.CommandText = $@"IF DB_ID('{databaseName}') IS NOT NULL
BEGIN
    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{databaseName}];
END
CREATE DATABASE [{databaseName}];";
            await command.ExecuteNonQueryAsync();
        }

        await IntegrationTestDatabase.ResetAsync(_factory);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.True(await verifyDb.EnvironmentMarkers.AnyAsync());
        var appliedMigrations = await verifyDb.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(appliedMigrations);
    }
}
