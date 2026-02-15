using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.IntegrationTests;

public class ListingDailyRateIntegrationTests : IntegrationTestBase
{
    public ListingDailyRateIntegrationTests(SqlServerTestDatabase database) : base(database)
    {
    }

    [Fact]
    public async Task SaveChanges_DuplicateTenantListingDate_ThrowsUniqueConstraintViolation()
    {
        var db = GetService<AppDbContext>();

        var property = new Property
        {
            Name = "Rate Property",
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
            Name = "Rate Listing",
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

        db.ListingDailyRates.Add(new ListingDailyRate
        {
            ListingId = listing.Id,
            Date = date,
            NightlyRate = 300m,
            Currency = "INR",
            Source = "Manual"
        });

        await db.SaveChangesAsync();

        db.ListingDailyRates.Add(new ListingDailyRate
        {
            ListingId = listing.Id,
            Date = date,
            NightlyRate = 350m,
            Currency = "INR",
            Source = "Manual"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
