using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Api.IntegrationTests;

public class DeleteBehaviorTests : IntegrationTestBase
{
    public DeleteBehaviorTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public void OnModelCreating_CascadesListingOnly()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = context.Model.FindEntityType(typeof(Booking))!;
        var fks = entity.GetForeignKeys();

        Assert.Equal(DeleteBehavior.Restrict, fks.Single(f => f.Properties.Any(p => p.Name == nameof(Booking.GuestId))).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, fks.Single(f => f.Properties.Any(p => p.Name == nameof(Booking.ListingId))).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, fks.Single(f => f.Properties.Any(p => p.Name == nameof(Booking.BankAccountId))).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, fks.Single(f => f.Properties.Any(p => p.Name == nameof(Booking.PropertyId))).DeleteBehavior);
    }
}
