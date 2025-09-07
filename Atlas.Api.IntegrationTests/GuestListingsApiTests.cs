using System.Net.Http.Json;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

public class GuestListingsApiTests : IntegrationTestBase
{
    public GuestListingsApiTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetList_ReturnsOnlyPublicListings()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var property = await DataSeeder.SeedPropertyAsync(db);

        var listingA = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Listing A",
            Floor = 1,
            Type = "Room",
            Status = "Active",
            WifiName = "wifi1",
            WifiPassword = "pass1",
            MaxGuests = 2,
            IsPublic = true,
            Slug = "listing-a",
            BlobPrefix = "101/",
            CoverImage = "cover.jpg"
        };
        listingA.Media.Add(new ListingMedia { BlobName = "cover.jpg", IsCover = true });
        listingA.Media.Add(new ListingMedia { BlobName = "bedroom/1.jpg", SortOrder = 2 });

        var listingB = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Listing B",
            Floor = 1,
            Type = "Room",
            Status = "Active",
            WifiName = "wifi2",
            WifiPassword = "pass2",
            MaxGuests = 2,
            IsPublic = false,
            Slug = "listing-b",
            BlobPrefix = "102/"
        };

        db.Listings.AddRange(listingA, listingB);
        await db.SaveChangesAsync();

        var response = await Client.GetAsync("/api/v1/guest/listings");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var listings = await response.Content.ReadFromJsonAsync<List<PublicListingDto>>();

        Assert.Single(listings!);
        Assert.Equal("listing-a", listings![0].Slug);
        Assert.DoesNotContain("wifi", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OwnerName", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CommissionPercent", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Availability_ExcludesUnpaidOrCancelled()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var property = await DataSeeder.SeedPropertyAsync(db);
        var guest = await DataSeeder.SeedGuestAsync(db);
        var listing = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Listing A",
            Floor = 1,
            Type = "Room",
            Status = "Active",
            WifiName = "wifi1",
            WifiPassword = "pass1",
            MaxGuests = 2,
            IsPublic = true,
            Slug = "listing-a",
            BlobPrefix = "101/"
        };
        db.Listings.Add(listing);
        await db.SaveChangesAsync();

        db.Bookings.AddRange(
            new Booking
            {
                ListingId = listing.Id,
                Listing = listing,
                GuestId = guest.Id,
                Guest = guest,
                BookingSource = "airbnb",
                PaymentStatus = "Paid",
                CheckinDate = new DateTime(2023, 9, 10),
                CheckoutDate = new DateTime(2023, 9, 12),
                AmountReceived = 100,
                Notes = "n"
            },
            new Booking
            {
                ListingId = listing.Id,
                Listing = listing,
                GuestId = guest.Id,
                Guest = guest,
                BookingSource = "airbnb",
                PaymentStatus = "Paid",
                CheckinDate = new DateTime(2023, 9, 15),
                CheckoutDate = new DateTime(2023, 9, 17),
                AmountReceived = 100,
                Notes = "n"
            },
            new Booking
            {
                ListingId = listing.Id,
                Listing = listing,
                GuestId = guest.Id,
                Guest = guest,
                BookingSource = "airbnb",
                PaymentStatus = "Cancelled",
                CheckinDate = new DateTime(2023, 9, 20),
                CheckoutDate = new DateTime(2023, 9, 22),
                AmountReceived = 0,
                Notes = "n"
            }
        );
        await db.SaveChangesAsync();

        var response = await Client.GetAsync("/api/v1/guest/listings/listing-a/availability?from=2023-09-01&to=2023-09-30");
        response.EnsureSuccessStatusCode();
        var days = await response.Content.ReadFromJsonAsync<List<DateTime>>();

        var expected = new[]
        {
            new DateTime(2023,9,10),
            new DateTime(2023,9,11),
            new DateTime(2023,9,15),
            new DateTime(2023,9,16)
        };
        Assert.Equal(expected, days);
    }
}
