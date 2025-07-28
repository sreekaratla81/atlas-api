using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Atlas.Api.Models;

namespace Atlas.Api.IntegrationTests;

public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    protected readonly CustomWebApplicationFactory Factory;
    protected HttpClient Client { get; }

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pendingMigrations = db.Database.GetPendingMigrations();
            if (pendingMigrations.Any())
            {
                throw new InvalidOperationException(
                    "You have pending migrations. Run 'dotnet ef migrations add' to create them.");
            }
            db.Database.Migrate();
        }

        Client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await ResetDatabase();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected async Task ResetDatabase()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

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

    protected T GetService<T>() where T : notnull
    {
        return Factory.Services.GetRequiredService<T>();
    }
}
