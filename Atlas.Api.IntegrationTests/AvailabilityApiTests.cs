using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

public class AvailabilityApiTests : IntegrationTestBase
{
    public AvailabilityApiTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetBookedDates_ReturnsOk_WithEmptyResult()
    {
        var response = await Client.GetAsync("/api/availability/booked-dates");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BookedDatesResponse>();
        Assert.NotNull(result);
        Assert.Empty(result.BookedDates);
    }

    [Fact]
    public async Task GetBookedDates_ReturnsBookedDates_ForSpecifiedListings()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var property = await DataSeeder.SeedPropertyAsync(db);
        var guest = await DataSeeder.SeedGuestAsync(db);
        
        var listing1 = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Listing 1",
            Floor = 1,
            Type = "Room",
            Status = "Active",
            WifiName = "wifi1",
            WifiPassword = "pass1",
            MaxGuests = 2,
            Slug = $"listing-1-{Guid.NewGuid():N}",
            BlobPrefix = $"{Guid.NewGuid():N}/"
        };
        var listing2 = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Listing 2",
            Floor = 2,
            Type = "Room",
            Status = "Active",
            WifiName = "wifi2",
            WifiPassword = "pass2",
            MaxGuests = 2,
            Slug = $"listing-2-{Guid.NewGuid():N}",
            BlobPrefix = $"{Guid.NewGuid():N}/"
        };
        db.Listings.AddRange(listing1, listing2);
        await db.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        db.Bookings.AddRange(
            new Booking
            {
                ListingId = listing1.Id,
                GuestId = guest.Id,
                BookingSource = "airbnb",
                PaymentStatus = "Paid",
                CheckinDate = today.AddDays(10),
                CheckoutDate = today.AddDays(12),
                AmountReceived = 100,
                Notes = "n"
            },
            new Booking
            {
                ListingId = listing2.Id,
                GuestId = guest.Id,
                BookingSource = "airbnb",
                PaymentStatus = "Paid",
                CheckinDate = today.AddDays(20),
                CheckoutDate = today.AddDays(22),
                AmountReceived = 100,
                Notes = "n"
            }
        );
        await db.SaveChangesAsync();

        var response = await Client.GetAsync($"/api/availability/booked-dates?listingIds={listing1.Id},{listing2.Id}");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BookedDatesResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result.BookedDates.Count);
        Assert.True(result.BookedDates.ContainsKey(listing1.Id));
        Assert.True(result.BookedDates.ContainsKey(listing2.Id));
        Assert.Single(result.BookedDates[listing1.Id]);
        Assert.Single(result.BookedDates[listing2.Id]);
    }

    [Fact]
    public async Task GetBookedDates_ExcludesCancelledBookings()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var property = await DataSeeder.SeedPropertyAsync(db);
        var guest = await DataSeeder.SeedGuestAsync(db);
        
        var listing = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Listing",
            Floor = 1,
            Type = "Room",
            Status = "Active",
            WifiName = "wifi",
            WifiPassword = "pass",
            MaxGuests = 2,
            Slug = $"listing-{Guid.NewGuid():N}",
            BlobPrefix = $"{Guid.NewGuid():N}/"
        };
        db.Listings.Add(listing);
        await db.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        db.Bookings.AddRange(
            new Booking
            {
                ListingId = listing.Id,
                GuestId = guest.Id,
                BookingSource = "airbnb",
                PaymentStatus = "Paid",
                CheckinDate = today.AddDays(10),
                CheckoutDate = today.AddDays(12),
                AmountReceived = 100,
                Notes = "n"
            },
            new Booking
            {
                ListingId = listing.Id,
                GuestId = guest.Id,
                BookingSource = "airbnb",
                PaymentStatus = "Cancelled",
                CheckinDate = today.AddDays(15),
                CheckoutDate = today.AddDays(17),
                AmountReceived = 0,
                Notes = "n"
            }
        );
        await db.SaveChangesAsync();

        var response = await Client.GetAsync($"/api/availability/booked-dates?listingIds={listing.Id}");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BookedDatesResponse>();
        Assert.NotNull(result);
        Assert.Single(result.BookedDates);
        Assert.True(result.BookedDates.ContainsKey(listing.Id));
        Assert.Single(result.BookedDates[listing.Id]);
        Assert.Equal(today.AddDays(10), result.BookedDates[listing.Id][0].CheckinDate);
    }

    [Fact]
    public async Task GetBookedDates_RespectsDaysAheadParameter()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var property = await DataSeeder.SeedPropertyAsync(db);
        var guest = await DataSeeder.SeedGuestAsync(db);
        
        var listing = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Listing",
            Floor = 1,
            Type = "Room",
            Status = "Active",
            WifiName = "wifi",
            WifiPassword = "pass",
            MaxGuests = 2,
            Slug = $"listing-{Guid.NewGuid():N}",
            BlobPrefix = $"{Guid.NewGuid():N}/"
        };
        db.Listings.Add(listing);
        await db.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        db.Bookings.AddRange(
            new Booking
            {
                ListingId = listing.Id,
                GuestId = guest.Id,
                BookingSource = "airbnb",
                PaymentStatus = "Paid",
                CheckinDate = today.AddDays(10),
                CheckoutDate = today.AddDays(12),
                AmountReceived = 100,
                Notes = "n"
            },
            new Booking
            {
                ListingId = listing.Id,
                GuestId = guest.Id,
                BookingSource = "airbnb",
                PaymentStatus = "Paid",
                CheckinDate = today.AddDays(200),
                CheckoutDate = today.AddDays(202),
                AmountReceived = 100,
                Notes = "n"
            }
        );
        await db.SaveChangesAsync();

        var response = await Client.GetAsync($"/api/availability/booked-dates?listingIds={listing.Id}&daysAhead=180");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BookedDatesResponse>();
        Assert.NotNull(result);
        Assert.Single(result.BookedDates);
        Assert.True(result.BookedDates.ContainsKey(listing.Id));
        Assert.Single(result.BookedDates[listing.Id]);
        Assert.Equal(today.AddDays(10), result.BookedDates[listing.Id][0].CheckinDate);
    }

    [Fact]
    public async Task GetBookedDates_UsesDateOverlapQuery()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var property = await DataSeeder.SeedPropertyAsync(db);
        var guest = await DataSeeder.SeedGuestAsync(db);
        
        var listing = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Listing",
            Floor = 1,
            Type = "Room",
            Status = "Active",
            WifiName = "wifi",
            WifiPassword = "pass",
            MaxGuests = 2,
            Slug = $"listing-{Guid.NewGuid():N}",
            BlobPrefix = $"{Guid.NewGuid():N}/"
        };
        db.Listings.Add(listing);
        await db.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        // Booking that starts before window but ends within
        db.Bookings.Add(new Booking
        {
            ListingId = listing.Id,
            GuestId = guest.Id,
            BookingSource = "airbnb",
            PaymentStatus = "Paid",
            CheckinDate = today.AddDays(-5),
            CheckoutDate = today.AddDays(5),
            AmountReceived = 100,
            Notes = "n"
        });
        // Booking that starts within window but ends after
        db.Bookings.Add(new Booking
        {
            ListingId = listing.Id,
            GuestId = guest.Id,
            BookingSource = "airbnb",
            PaymentStatus = "Paid",
            CheckinDate = today.AddDays(175),
            CheckoutDate = today.AddDays(185),
            AmountReceived = 100,
            Notes = "n"
        });
        // Booking completely outside window
        db.Bookings.Add(new Booking
        {
            ListingId = listing.Id,
            GuestId = guest.Id,
            BookingSource = "airbnb",
            PaymentStatus = "Paid",
            CheckinDate = today.AddDays(200),
            CheckoutDate = today.AddDays(202),
            AmountReceived = 100,
            Notes = "n"
        });
        await db.SaveChangesAsync();

        var response = await Client.GetAsync($"/api/availability/booked-dates?listingIds={listing.Id}&daysAhead=180");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BookedDatesResponse>();
        Assert.NotNull(result);
        Assert.Single(result.BookedDates);
        Assert.True(result.BookedDates.ContainsKey(listing.Id));
        Assert.Equal(2, result.BookedDates[listing.Id].Count); // Should include overlapping bookings
    }

    [Fact]
    public async Task GetBookedDates_ReturnsAllListings_WhenNoListingIdsProvided()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var property = await DataSeeder.SeedPropertyAsync(db);
        var guest = await DataSeeder.SeedGuestAsync(db);
        
        var listing1 = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Listing 1",
            Floor = 1,
            Type = "Room",
            Status = "Active",
            WifiName = "wifi1",
            WifiPassword = "pass1",
            MaxGuests = 2,
            Slug = $"listing-1-{Guid.NewGuid():N}",
            BlobPrefix = $"{Guid.NewGuid():N}/"
        };
        var listing2 = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Listing 2",
            Floor = 2,
            Type = "Room",
            Status = "Active",
            WifiName = "wifi2",
            WifiPassword = "pass2",
            MaxGuests = 2,
            Slug = $"listing-2-{Guid.NewGuid():N}",
            BlobPrefix = $"{Guid.NewGuid():N}/"
        };
        db.Listings.AddRange(listing1, listing2);
        await db.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        db.Bookings.AddRange(
            new Booking
            {
                ListingId = listing1.Id,
                GuestId = guest.Id,
                BookingSource = "airbnb",
                PaymentStatus = "Paid",
                CheckinDate = today.AddDays(10),
                CheckoutDate = today.AddDays(12),
                AmountReceived = 100,
                Notes = "n"
            },
            new Booking
            {
                ListingId = listing2.Id,
                GuestId = guest.Id,
                BookingSource = "airbnb",
                PaymentStatus = "Paid",
                CheckinDate = today.AddDays(20),
                CheckoutDate = today.AddDays(22),
                AmountReceived = 100,
                Notes = "n"
            }
        );
        await db.SaveChangesAsync();

        var response = await Client.GetAsync("/api/availability/booked-dates");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BookedDatesResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result.BookedDates.Count);
        Assert.True(result.BookedDates.ContainsKey(listing1.Id));
        Assert.True(result.BookedDates.ContainsKey(listing2.Id));
    }
}

