using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

public class ListingMediaDbTests : IntegrationTestBase
{
    public ListingMediaDbTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task OnlyOneCoverImagePerListing()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Listing",
            Floor = 0,
            Type = "Room",
            Status = "Active",
            WifiName = "wifi",
            WifiPassword = "pass",
            MaxGuests = 2,
            IsPublic = true,
            Slug = "listing",
            BlobPrefix = "201/"
        };
        db.Listings.Add(listing);
        await db.SaveChangesAsync();

        db.ListingMedia.Add(new ListingMedia { ListingId = listing.Id, BlobName = "cover1.jpg", IsCover = true });
        await db.SaveChangesAsync();

        db.ListingMedia.Add(new ListingMedia { ListingId = listing.Id, BlobName = "cover2.jpg", IsCover = true });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
