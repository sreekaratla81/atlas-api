using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Respawn.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;

namespace Atlas.Api.IntegrationTests;

[Collection("IntegrationTests")]
public abstract class IntegrationTestBase : IClassFixture<SqlServerTestDatabase>, IAsyncLifetime
{
    protected CustomWebApplicationFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    private readonly SqlServerTestDatabase _database;
    private TransactionScope? _testTransaction;

    protected IntegrationTestBase(SqlServerTestDatabase database)
    {
        _database = database;
    }

    protected string ApiRoute(string relativePath) => Factory.ApiRoute(relativePath);

    protected string ApiControllerRoute(string relativePath) => ApiRoute($"api/{relativePath.TrimStart('/')}");

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("Atlas_TestDb", _database.ConnectionString);
        Factory = new CustomWebApplicationFactory(_database.ConnectionString);
        Client = Factory.CreateClient();

        await IntegrationTestDatabase.ResetAsync(Factory);
        _testTransaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
    }

    public Task DisposeAsync()
    {
        _testTransaction?.Dispose();
        _testTransaction = null;
        Client.Dispose();
        Factory.Dispose();
        return Task.CompletedTask;
    }

    protected T GetService<T>() where T : notnull
    {
        return Factory.Services.GetRequiredService<T>();
    }
}

internal static class IntegrationTestDatabase
{
    private static Respawner? _respawner;
    private static readonly SemaphoreSlim RespawnerSemaphore = new(1, 1);

    internal static async Task ResetAsync(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = (SqlConnection)db.Database.GetDbConnection();

        await connection.OpenAsync();

        var isDatabaseEmpty = await IsDatabaseEmptyAsync(db, connection);
        await EnsureRespawnerAsync(db, connection, isDatabaseEmpty);
        await _respawner!.ResetAsync(connection);

        await SeedBaselineDataAsync(db);
    }

    private static async Task EnsureRespawnerAsync(
        AppDbContext db,
        SqlConnection connection,
        bool forceRecreate)
    {
        if (_respawner != null && !forceRecreate)
        {
            return;
        }

        await RespawnerSemaphore.WaitAsync();
        try
        {
            if (_respawner != null && !forceRecreate)
            {
                return;
            }

            await DatabaseSchemaInitializer.EnsureSchemaAsync(db.Database);

            var isDatabaseEmpty = await IsDatabaseEmptyAsync(db, connection);
            if (isDatabaseEmpty)
            {
                var migrations = (await db.Database.GetMigrationsAsync()).ToList();
                if (migrations.Count > 0)
                {
                    await db.Database.MigrateAsync();
                }
                else
                {
                    await db.Database.EnsureCreatedAsync();
                }

                isDatabaseEmpty = await IsDatabaseEmptyAsync(db, connection);
                if (isDatabaseEmpty)
                {
                    var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync()).ToList();
                    var diagnostic = BuildMissingTablesDiagnostic(
                        connection.Database,
                        GetModelTableNames(db),
                        migrations,
                        appliedMigrations);
                    throw new InvalidOperationException(diagnostic);
                }
            }

            _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer,
                SchemasToInclude = new[] { "dbo" },
                TablesToIgnore = new Table[] { "__EFMigrationsHistory" }
            });
        }
        finally
        {
            RespawnerSemaphore.Release();
        }
    }

    private static async Task<bool> IsDatabaseEmptyAsync(AppDbContext db, SqlConnection connection)
    {
        var modelTables = GetModelTableNames(db);
        if (modelTables.Count == 0)
        {
            return true;
        }

        await using var command = connection.CreateCommand();
        var conditions = new List<string>(modelTables.Count);

        for (var index = 0; index < modelTables.Count; index++)
        {
            var table = modelTables[index];
            var schemaParameter = $"@schema{index}";
            var tableParameter = $"@table{index}";
            conditions.Add($"(TABLE_SCHEMA = {schemaParameter} AND TABLE_NAME = {tableParameter})");
            command.Parameters.AddWithValue(schemaParameter, table.Schema);
            command.Parameters.AddWithValue(tableParameter, table.Table);
        }

        command.CommandText = $"""
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'
  AND ({string.Join(" OR ", conditions)})
""";

        var result = await command.ExecuteScalarAsync();
        var tableCount = Convert.ToInt32(result ?? 0);
        return tableCount == 0;
    }

    internal static IReadOnlyList<(string Schema, string Table)> GetModelTableNames(AppDbContext db)
    {
        return db.Model.GetEntityTypes()
            .Select(entityType => new
            {
                Schema = entityType.GetSchema() ?? "dbo",
                Table = entityType.GetTableName()
            })
            .Where(table => !string.IsNullOrWhiteSpace(table.Table))
            .Where(table => !string.Equals(table.Table, "__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase))
            .Select(table => (table.Schema, Table: table.Table!))
            .Distinct()
            .ToList();
    }

    internal static string BuildMissingTablesDiagnostic(
        string databaseName,
        IReadOnlyList<(string Schema, string Table)> modelTables,
        IReadOnlyCollection<string> migrations,
        IReadOnlyCollection<string> appliedMigrations)
    {
        var tableList = modelTables.Count == 0
            ? "<none>"
            : string.Join(", ", modelTables.Select(table => $"{table.Schema}.{table.Table}"));
        var migrationList = migrations.Count == 0 ? "<none>" : string.Join(", ", migrations);
        var appliedList = appliedMigrations.Count == 0 ? "<none>" : string.Join(", ", appliedMigrations);

        return $"Respawner initialization failed because no tables were found in database '{databaseName}' after schema initialization. " +
            $"Model tables: {tableList}. " +
            $"Migrations: {migrationList}. " +
            $"Applied migrations: {appliedList}. " +
            "Ensure migrations are available and applied before creating the respawner.";
    }

    private static async Task SeedBaselineDataAsync(AppDbContext db)
    {
        if (!await db.EnvironmentMarkers.AnyAsync())
        {
            db.EnvironmentMarkers.Add(new EnvironmentMarker
            {
                Marker = "DEV"
            });
            await db.SaveChangesAsync();
        }

        if (!await db.Properties.AnyAsync())
        {
            db.Properties.Add(new Property
            {
                Name = "Test Villa",
                Address = "Seed Address",
                Type = "Villa",
                OwnerName = "Owner",
                ContactPhone = "000",
                CommissionPercent = 10,
                Status = "Active"
            });
            await db.SaveChangesAsync();
        }
    }
}
