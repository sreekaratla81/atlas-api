using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Atlas.Api.Tests;

public class DeleteBehaviorTests
{
    [Fact]
    public void OnModelCreating_UsesCascadeOnListingOnly()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("DeleteBehaviorTest")
            .Options;

        using var context = new AppDbContext(options);
        var entity = context.Model.FindEntityType(typeof(Booking))!;
        var fks = entity.GetForeignKeys();

        Assert.Equal(DeleteBehavior.Restrict, fks.Single(f => f.Properties.Any(p => p.Name == nameof(Booking.GuestId))).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, fks.Single(f => f.Properties.Any(p => p.Name == nameof(Booking.ListingId))).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, fks.Single(f => f.Properties.Any(p => p.Name == nameof(Booking.BankAccountId))).DeleteBehavior);
    }
}
