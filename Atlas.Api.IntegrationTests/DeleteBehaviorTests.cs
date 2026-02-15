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
    public void AutomationSchedule_UsesCascadeBookingFkAndUniqueCompositeIndexInIntegration()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = context.Model.FindEntityType(typeof(AutomationSchedule))!;

        var bookingForeignKey = Assert.Single(entity.GetForeignKeys(), fk => fk.Properties.Single().Name == nameof(AutomationSchedule.BookingId));
        Assert.Equal(DeleteBehavior.Cascade, bookingForeignKey.DeleteBehavior);

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(AutomationSchedule.TenantId),
                nameof(AutomationSchedule.BookingId),
                nameof(AutomationSchedule.EventType),
                nameof(AutomationSchedule.DueAtUtc)
            }));
    }

}
