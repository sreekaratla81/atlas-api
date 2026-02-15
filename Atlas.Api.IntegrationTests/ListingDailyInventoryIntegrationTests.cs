using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.IntegrationTests;

public class ListingDailyInventoryIntegrationTests : IntegrationTestBase
{
    public ListingDailyInventoryIntegrationTests(SqlServerTestDatabase database) : base(database)
    {
    }

    [Fact]
    public async Task SaveChanges_DuplicateTenantListingDate_ThrowsUniqueConstraintViolation()
    {
        var db = GetService<AppDbContext>();

        var property = new Property
        {
            Name = "Inventory Property",
            Address = "Test Address",
            Type = "Apartment",
            OwnerName = "Owner",
            ContactPhone = "1234567890",
            Status = "Active"
        };

        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var listing = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Inventory Listing",
            Floor = 1,
            Type = "Studio",
            Status = "Active",
            WifiName = "wifi",
            WifiPassword = "password",
            MaxGuests = 2
        };

        db.Listings.Add(listing);
        await db.SaveChangesAsync();

        var date = new DateTime(2026, 1, 5);

        db.ListingDailyInventories.Add(new ListingDailyInventory
        {
            ListingId = listing.Id,
            Date = date,
            RoomsAvailable = 2,
            Source = "Manual"
        });

        await db.SaveChangesAsync();

        db.ListingDailyInventories.Add(new ListingDailyInventory
        {
            ListingId = listing.Id,
            Date = date,
            RoomsAvailable = 1,
            Source = "Manual"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_NegativeRoomsAvailable_ThrowsDbUpdateException()
    {
        var db = GetService<AppDbContext>();

        var property = new Property
        {
            Name = "Constraint Property",
            Address = "Test Address",
            Type = "Apartment",
            OwnerName = "Owner",
            ContactPhone = "1234567890",
            Status = "Active"
        };

        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var listing = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Constraint Listing",
            Floor = 2,
            Type = "Studio",
            Status = "Active",
            WifiName = "wifi",
            WifiPassword = "password",
            MaxGuests = 2
        };

        db.Listings.Add(listing);
        await db.SaveChangesAsync();

        db.ListingDailyInventories.Add(new ListingDailyInventory
        {
            ListingId = listing.Id,
            Date = new DateTime(2026, 1, 6),
            RoomsAvailable = -1,
            Source = "Manual"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

}
