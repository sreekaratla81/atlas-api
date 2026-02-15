using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Api.IntegrationTests;

public class DeleteBehaviorTests : IntegrationTestBase
{
    public DeleteBehaviorTests(SqlServerTestDatabase database) : base(database) { }

    [Fact]
    public void OnModelCreating_UsesCascadeDeletesInIntegrationTests()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = context.Model.FindEntityType(typeof(Booking))!;
        var fks = entity.GetForeignKeys();

        Assert.All(fks, fk => Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior));
    }

    [Fact]
    public void WhatsAppInboundMessage_ForeignKeys_UseCascadeInIntegrationTests()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = context.Model.FindEntityType(typeof(WhatsAppInboundMessage))!;

        Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior));
    }
}
