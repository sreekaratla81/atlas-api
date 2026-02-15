using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Atlas.Api.Tests;

public class ConsumedEventModelTests
{
    [Fact]
    public void OnModelCreating_ConfiguresConsumedEventDedupeAndRetentionIndexes()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("ConsumedEventModelTests")
            .Options;

        using var context = new AppDbContext(options);
        var entity = context.Model.FindEntityType(typeof(ConsumedEvent));

        Assert.NotNull(entity);

        var dedupeIndex = entity!.GetIndexes().SingleOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "ConsumerName", "EventId" }));
        Assert.NotNull(dedupeIndex);
        Assert.True(dedupeIndex!.IsUnique);

        var retentionIndex = entity.GetIndexes().SingleOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "ProcessedAtUtc" }));
        Assert.NotNull(retentionIndex);
    }
}
