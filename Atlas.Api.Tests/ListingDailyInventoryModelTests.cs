using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Tests;

public class ListingDailyInventoryModelTests
{
    [Fact]
    public void ListingDailyInventory_HasExpectedModelConfiguration()
    {
        // Use SqlServer so GetColumnType() (relational) works; InMemory does not provide RelationalTypeMapping.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Dummy;Trusted_Connection=True;")
            .Options;

        using var context = new AppDbContext(options);

        var entity = context.Model.FindEntityType(typeof(ListingDailyInventory));

        Assert.NotNull(entity);
        Assert.Equal("ListingDailyInventory", entity!.GetTableName());

        var source = entity.FindProperty(nameof(ListingDailyInventory.Source));
        Assert.NotNull(source);
        Assert.Equal(20, source!.GetMaxLength());

        var date = entity.FindProperty(nameof(ListingDailyInventory.Date));
        Assert.NotNull(date);
        Assert.Equal("date", date!.GetColumnType());

        var uniqueIndex = entity.GetIndexes().SingleOrDefault(idx =>
            idx.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(ListingDailyInventory.TenantId),
                nameof(ListingDailyInventory.ListingId),
                nameof(ListingDailyInventory.Date)
            }));

        Assert.NotNull(uniqueIndex);
        Assert.True(uniqueIndex!.IsUnique);
    }
}
