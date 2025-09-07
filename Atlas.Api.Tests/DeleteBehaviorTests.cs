using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Linq;

namespace Atlas.Api.Tests;

public class DeleteBehaviorTests
{
    [Fact]
    public void OnModelCreating_RestrictsDeletes()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("DeleteBehaviorTest")
            .Options;

        using var context = new AppDbContext(options);
        var entity = context.Model.FindEntityType(typeof(Booking))!;
        var fks = entity.GetForeignKeys();
        var guestFk = fks.Single(fk => fk.PrincipalEntityType.ClrType == typeof(Guest));
        Assert.Equal(DeleteBehavior.Cascade, guestFk.DeleteBehavior);
        Assert.All(fks.Where(fk => fk != guestFk), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }
}
