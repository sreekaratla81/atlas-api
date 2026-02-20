using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Atlas.Api.Tests;

public class DeleteBehaviorTests
{
    [Fact]
    public void OnModelCreating_RestrictsDeletesByDefault()
    {
        Environment.SetEnvironmentVariable("ATLAS_DELETE_BEHAVIOR", null);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("DeleteBehaviorTest")
            .Options;

        using var context = new AppDbContext(options);
        var entity = context.Model.FindEntityType(typeof(Booking))!;
        var fks = entity.GetForeignKeys();

        Assert.All(fks, fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }

    [Fact]
    public void AutomationSchedule_UsesBookingFkAndUniqueCompositeIndex()
    {
        Environment.SetEnvironmentVariable("ATLAS_DELETE_BEHAVIOR", null);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new AppDbContext(options);
        var entity = context.Model.FindEntityType(typeof(AutomationSchedule))!;

        var bookingForeignKey = Assert.Single(entity.GetForeignKeys(), fk => fk.Properties.Single().Name == nameof(AutomationSchedule.BookingId));
        Assert.Equal(DeleteBehavior.Restrict, bookingForeignKey.DeleteBehavior);

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(AutomationSchedule.TenantId),
                nameof(AutomationSchedule.BookingId),
                nameof(AutomationSchedule.EventType),
                nameof(AutomationSchedule.DueAtUtc)
            }));

        Assert.DoesNotContain(entity.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(AutomationSchedule.TenantId),
                nameof(AutomationSchedule.BookingId),
                nameof(AutomationSchedule.DueAtUtc)
            }));
    }

    [Fact]
    public void WhatsAppInboundMessage_UsesExpectedIndexesAndForeignKeys()
    {
        Environment.SetEnvironmentVariable("ATLAS_DELETE_BEHAVIOR", null);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new AppDbContext(options);
        var entity = context.Model.FindEntityType(typeof(WhatsAppInboundMessage))!;

        var bookingForeignKey = Assert.Single(entity.GetForeignKeys(), fk => fk.Properties.Single().Name == nameof(WhatsAppInboundMessage.BookingId));
        Assert.Equal(DeleteBehavior.Restrict, bookingForeignKey.DeleteBehavior);

        var guestForeignKey = Assert.Single(entity.GetForeignKeys(), fk => fk.Properties.Single().Name == nameof(WhatsAppInboundMessage.GuestId));
        Assert.Equal(DeleteBehavior.Restrict, guestForeignKey.DeleteBehavior);

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(WhatsAppInboundMessage.TenantId),
                nameof(WhatsAppInboundMessage.Provider),
                nameof(WhatsAppInboundMessage.ProviderMessageId)
            }));

        Assert.Contains(entity.GetIndexes(), index =>
            !index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(WhatsAppInboundMessage.TenantId),
                nameof(WhatsAppInboundMessage.ReceivedAtUtc)
            }));
    }


    [Fact]
    public void ConsumedEvent_UsesExpectedIndexesAndTenantForeignKey()
    {
        Environment.SetEnvironmentVariable("ATLAS_DELETE_BEHAVIOR", null);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new AppDbContext(options);
        var entity = context.Model.FindEntityType(typeof(ConsumedEvent))!;

        var tenantForeignKey = Assert.Single(entity.GetForeignKeys(), fk => fk.Properties.Single().Name == nameof(ConsumedEvent.TenantId));
        Assert.Equal(DeleteBehavior.Restrict, tenantForeignKey.DeleteBehavior);

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(ConsumedEvent.TenantId),
                nameof(ConsumedEvent.ConsumerName),
                nameof(ConsumedEvent.EventId)
            }));

        Assert.Contains(entity.GetIndexes(), index =>
            !index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(ConsumedEvent.TenantId),
                nameof(ConsumedEvent.ProcessedAtUtc)
            }));
    }

}
