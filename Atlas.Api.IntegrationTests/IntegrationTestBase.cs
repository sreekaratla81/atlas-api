using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Respawn.Graph;

namespace Atlas.Api.IntegrationTests;

[Collection("IntegrationTests")]
public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    protected readonly CustomWebApplicationFactory Factory;
    protected HttpClient Client { get; }

    private static Respawner? _respawner;
    private static readonly SemaphoreSlim RespawnerSemaphore = new(1, 1);

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;

        Client = factory.CreateClient();
    }

    protected string ApiRoute(string relativePath) => Factory.ApiRoute(relativePath);

    public async Task InitializeAsync()
    {
        await ResetDatabase();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected async Task ResetDatabase()
    {
        using var scope = Factory.Services.CreateScope();
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

            await db.Database.MigrateAsync();

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
        if (await db.Properties.AnyAsync())
        {
            return;
        }

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

    protected T GetService<T>() where T : notnull
    {
        return Factory.Services.GetRequiredService<T>();
    }
}
