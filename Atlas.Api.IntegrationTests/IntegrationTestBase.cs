using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Respawn.Graph;
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

        await EnsureRespawnerAsync(db, connection);
        await _respawner!.ResetAsync(connection);

        await SeedBaselineDataAsync(db);
    }

    private static async Task EnsureRespawnerAsync(AppDbContext db, SqlConnection connection)
    {
        if (_respawner != null)
        {
            return;
        }

        await RespawnerSemaphore.WaitAsync();
        try
        {
            if (_respawner != null)
            {
                return;
            }

            await DatabaseSchemaInitializer.EnsureSchemaAsync(db.Database);

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
