using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using Xunit;

namespace Atlas.Api.IntegrationTests;

public class DeleteBehaviorTests : IntegrationTestBase
{
    public DeleteBehaviorTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public void OnModelCreating_CascadeOnlyListing()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = context.Model.FindEntityType(typeof(Booking))!;
        var fks = entity.GetForeignKeys().ToList();

        var listingFk = fks.Single(fk => fk.PrincipalEntityType.ClrType == typeof(Listing));
        Assert.Equal(DeleteBehavior.Cascade, listingFk.DeleteBehavior);
        Assert.All(fks.Where(fk => fk != listingFk), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }
}
