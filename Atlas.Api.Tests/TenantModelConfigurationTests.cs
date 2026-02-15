using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Tests;

public class TenantModelConfigurationTests
{
    [Fact]
    public void Tenant_UsesCanonicalTenantsTableName()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new AppDbContext(options);

        var entity = context.Model.FindEntityType(typeof(Tenant));

        Assert.NotNull(entity);
        Assert.Equal("Tenants", entity!.GetTableName());
    }
}
