using System;
using Atlas.Api.Data;
using Atlas.Api.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Api.Tests;

public class DeleteBehaviorTests
{
    [Fact]
    public void OnModelCreating_RestrictOutsideIntegrationTest()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        var provider = services.BuildServiceProvider();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .UseInternalServiceProvider(provider)
            .Options;

        using var context = new AppDbContext(options);
        var entity = context.Model.FindEntityType(typeof(Booking))!;
        var fks = entity.GetForeignKeys();

        Assert.All(fks, fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }

    [Fact]
    public void OnModelCreating_CascadeOnlyListingInIntegrationTest()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "IntegrationTest");
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        var provider = services.BuildServiceProvider();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .UseInternalServiceProvider(provider)
            .Options;

        using var context = new AppDbContext(options);
        var entity = context.Model.FindEntityType(typeof(Booking))!;
        var fks = entity.GetForeignKeys().ToList();

        var listingFk = fks.Single(fk => fk.PrincipalEntityType.ClrType == typeof(Listing));
        Assert.Equal(DeleteBehavior.Cascade, listingFk.DeleteBehavior);
        Assert.All(fks.Where(fk => fk != listingFk), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }
}
