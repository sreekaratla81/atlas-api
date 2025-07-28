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
    public void OnModelCreating_AllCascadeInIntegrationTest()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = context.Model.FindEntityType(typeof(Booking))!;

        foreach (var fk in entity.GetForeignKeys())
        {
            Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
        }
    }
}
