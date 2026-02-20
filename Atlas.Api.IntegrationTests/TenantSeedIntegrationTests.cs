using Atlas.Api.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Api.IntegrationTests;

public class TenantSeedIntegrationTests : IntegrationTestBase
{
    public TenantSeedIntegrationTests(SqlServerTestDatabase database) : base(database)
    {
    }

    [Fact]
    public void DefaultAtlasTenant_ExistsAfterMigrations()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = dbContext.Tenants.SingleOrDefault(t => t.Slug == "atlas");

        Assert.NotNull(tenant);
    }
}
