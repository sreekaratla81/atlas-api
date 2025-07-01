using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    protected readonly CustomWebApplicationFactory Factory;
    protected HttpClient Client => Factory.CreateClient();

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
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
        await db.Database.EnsureCreatedAsync();
    }

    protected T GetService<T>() where T : notnull
    {
        return Factory.Services.GetRequiredService<T>();
    }
}
