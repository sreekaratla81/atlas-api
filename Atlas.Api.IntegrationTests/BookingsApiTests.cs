using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

public class BookingsApiTests : IntegrationTestBase
{
    public BookingsApiTests(CustomWebApplicationFactory factory) : base(factory) {}

    private async Task<(Property property, Listing listing, Guest guest, Booking booking)> SeedBookingAsync(AppDbContext db)
    {
        var property = new Property
        {
            Name = "Test Property",
            Address = "123 Street",
            Type = "House",
            OwnerName = "Owner",
            ContactPhone = "555-0000",
            CommissionPercent = 10,
            Status = "Active"
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();
        var listing = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Test Listing",
            Floor = 1,
            Type = "Room",
            Status = "Available",
            WifiName = "wifi",
            WifiPassword = "pass",
            MaxGuests = 2
        };
        db.Listings.Add(listing);
        var guest = new Guest
        {
            Name = "Guest",
            Phone = "123456",
            Email = "guest@example.com",
            IdProofUrl = "N/A"
        };
        db.Guests.Add(guest);
        await db.SaveChangesAsync();
        var booking = new Booking
        {
            ListingId = listing.Id,
            GuestId = guest.Id,
            BookingSource = "airbnb",
            PaymentStatus = "Pending",
            CheckinDate = DateTime.UtcNow.Date,
            CheckoutDate = DateTime.UtcNow.Date.AddDays(1),
            AmountReceived = 100,
            GuestsPlanned = 1,
            GuestsActual = 1,
            ExtraGuestCharge = 0,
            AmountGuestPaid = 100,
            CommissionAmount = 0,
            Notes = "note",
            BankAccountId = null
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return (property, listing, guest, booking);
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedBookingAsync(db);

        var response = await Client.GetAsync("/api/bookings");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenMissing()
    {
        var response = await Client.GetAsync("/api/bookings/1");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_CreatesBooking()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedBookingAsync(db);

        var newBooking = new
        {
            listingId = data.listing.Id,
            guestId = data.guest.Id,
            checkinDate = DateTime.UtcNow.Date,
            checkoutDate = DateTime.UtcNow.Date.AddDays(2),
            bookingSource = "airbnb",
            paymentStatus = "Pending",
            amountReceived = 200m,
            bankAccountId = (int?)null,
            guestsPlanned = 2,
            guestsActual = 2,
            extraGuestCharge = 0m,
            notes = "create"
        };

        var response = await Client.PostAsJsonAsync("/api/bookings", newBooking);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db2.Bookings.CountAsync();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Post_CreatesBookingWithoutNotes()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedBookingAsync(db);

        var newBooking = new
        {
            listingId = data.listing.Id,
            guestId = data.guest.Id,
            checkinDate = DateTime.UtcNow.Date,
            checkoutDate = DateTime.UtcNow.Date.AddDays(2),
            bookingSource = "airbnb",
            paymentStatus = "Pending",
            amountReceived = 200m,
            bankAccountId = (int?)null,
            guestsPlanned = 2,
            guestsActual = 2,
            extraGuestCharge = 0m
        };

        var response = await Client.PostAsJsonAsync("/api/bookings", newBooking);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Post_CreatesBooking_WhenPaymentStatusMissing_Alt()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedBookingAsync(db);

        var newBooking = new
        {
            listingId = data.listing.Id,
            guestId = data.guest.Id,
            checkinDate = DateTime.UtcNow.Date,
            checkoutDate = DateTime.UtcNow.Date.AddDays(2),
            bookingSource = "airbnb",
            amountReceived = 200m,
            bankAccountId = (int?)null,
            guestsPlanned = 2,
            guestsActual = 2,
            extraGuestCharge = 0m,
            notes = "create"
        };

        var response = await Client.PostAsJsonAsync("/api/bookings", newBooking);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Put_UpdatesBooking()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedBookingAsync(db);
        var id = data.booking.Id;



        var payload = new
        {
            id = id,
            listingId = data.listing.Id,
            guestId = data.guest.Id,
            propertyId = data.property.Id,
            checkinDate = data.booking.CheckinDate,
            checkoutDate = data.booking.CheckoutDate,
            bookingSource = data.booking.BookingSource,
            amountReceived = data.booking.AmountReceived,
            bankAccountId = data.booking.BankAccountId,
            guestsPlanned = data.booking.GuestsPlanned,
            guestsActual = data.booking.GuestsActual,
            extraGuestCharge = data.booking.ExtraGuestCharge,
            amountGuestPaid = data.booking.AmountGuestPaid,
            commissionAmount = data.booking.CommissionAmount,
            paymentStatus = data.booking.PaymentStatus,
            notes = "updated"
        };
        var response = await Client.PutAsJsonAsync($"/api/bookings/{id}", payload);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await db2.Bookings.FindAsync(id);
        Assert.Equal("updated", updated!.Notes);
    }

    [Fact]
    public async Task Put_UpdatesBooking_WhenPaymentStatusMissing_Alt()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedBookingAsync(db);
        var id = data.booking.Id;

        var payload = new
        {
            id = id,
            listingId = data.listing.Id,
            guestId = data.guest.Id,
            propertyId = data.property.Id,
            checkinDate = data.booking.CheckinDate,
            checkoutDate = data.booking.CheckoutDate,
            bookingSource = data.booking.BookingSource,
            amountReceived = data.booking.AmountReceived,
            bankAccountId = data.booking.BankAccountId,
            guestsPlanned = data.booking.GuestsPlanned,
            guestsActual = data.booking.GuestsActual,
            extraGuestCharge = data.booking.ExtraGuestCharge,
            amountGuestPaid = data.booking.AmountGuestPaid,
            commissionAmount = data.booking.CommissionAmount,
            notes = "bad"
        };

        var response = await Client.PutAsJsonAsync($"/api/bookings/{id}", payload);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Put_ReturnsBadRequest_OnIdMismatch()
    {
        var booking = new
        {
            id = 1,
            listingId = 1,
            guestId = 1,
            propertyId = 1,
            checkinDate = DateTime.UtcNow,
            checkoutDate = DateTime.UtcNow,
            bookingSource = "a",
            paymentStatus = "p",
            amountReceived = 0m,
            bankAccountId = (int?)null,
            guestsPlanned = 0,
            guestsActual = 0,
            extraGuestCharge = 0m,
            amountGuestPaid = 0m,
            commissionAmount = 0m,
            notes = "n"
        };
        var response = await Client.PutAsJsonAsync("/api/bookings/2", booking);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_CreatesBooking_WhenPaymentStatusMissing()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedBookingAsync(db);

        var request = new
        {
            ListingId = data.listing.Id,
            GuestId = data.guest.Id,
            BookingSource = "airbnb",
            CheckinDate = DateTime.UtcNow.Date,
            CheckoutDate = DateTime.UtcNow.Date.AddDays(2),
            AmountReceived = 200,
            GuestsPlanned = 2,
            GuestsActual = 2,
            ExtraGuestCharge = 0,
            Notes = "create"
        };

        var response = await Client.PostAsJsonAsync("/api/bookings", request);

        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Put_UpdatesBooking_WhenPaymentStatusMissing()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedBookingAsync(db);

        var request = new
        {
            Id = data.booking.Id,
            ListingId = data.booking.ListingId,
            GuestId = data.booking.GuestId,
            BookingSource = data.booking.BookingSource,
            CheckinDate = data.booking.CheckinDate,
            CheckoutDate = data.booking.CheckoutDate,
            AmountReceived = data.booking.AmountReceived,
            GuestsPlanned = data.booking.GuestsPlanned,
            GuestsActual = data.booking.GuestsActual,
            ExtraGuestCharge = data.booking.ExtraGuestCharge,
            AmountGuestPaid = data.booking.AmountGuestPaid,
            CommissionAmount = data.booking.CommissionAmount,
            Notes = "updated",
            BankAccountId = data.booking.BankAccountId
        };

        var response = await Client.PutAsJsonAsync($"/api/bookings/{data.booking.Id}", request);

        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Post_ReturnsCreated_WhenPaymentStatusProvided()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedBookingAsync(db);

        var request = new
        {
            ListingId = data.listing.Id,
            GuestId = data.guest.Id,
            BookingSource = "airbnb",
            PaymentStatus = "Paid",
            CheckinDate = DateTime.UtcNow.Date,
            CheckoutDate = DateTime.UtcNow.Date.AddDays(3),
            AmountReceived = 250,
            GuestsPlanned = 2,
            GuestsActual = 2,
            ExtraGuestCharge = 0,
            Notes = "full"
        };

        var response = await Client.PostAsJsonAsync("/api/bookings", request);

        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesBooking()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedBookingAsync(db);
        var id = data.booking.Id;

        var response = await Client.DeleteAsync($"/api/bookings/{id}");
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var exists = await db2.Bookings.AnyAsync(b => b.Id == id);
        Assert.False(exists);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        var response = await Client.DeleteAsync("/api/bookings/1");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
