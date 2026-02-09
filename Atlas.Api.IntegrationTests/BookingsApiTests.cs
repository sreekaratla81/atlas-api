using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace Atlas.Api.IntegrationTests;

[Trait("Suite", "Contract")]
public class BookingsApiTests : IntegrationTestBase
{
    public BookingsApiTests(SqlServerTestDatabase database) : base(database) {}

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

        var response = await Client.GetAsync(ApiRoute("bookings"));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ProjectsGuestName()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedBookingAsync(db);

        var response = await Client.GetAsync(ApiRoute("bookings"));
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("guest", json);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenMissing()
    {
        var response = await Client.GetAsync(ApiRoute("bookings/1"));
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
            bookingStatus = "Confirmed",
            totalAmount = 350m,
            currency = "USD",
            externalReservationId = "EXT-BOOK-1",
            confirmationSentAtUtc = DateTime.UtcNow,
            paymentStatus = "Pending",
            amountReceived = 200m,
            bankAccountId = (int?)null,
            guestsPlanned = 2,
            guestsActual = 2,
            extraGuestCharge = 0m,
            notes = "create"
        };

        var response = await Client.PostAsJsonAsync(ApiRoute("bookings"), newBooking);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db2.Bookings.CountAsync();
        Assert.Equal(2, count);
        var created = await db2.Bookings.OrderByDescending(b => b.Id).FirstAsync();
        Assert.Equal("Confirmed", created.BookingStatus);
        Assert.Equal(350m, created.TotalAmount);
        Assert.Equal("USD", created.Currency);
        Assert.Equal("EXT-BOOK-1", created.ExternalReservationId);
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

        var response = await Client.PostAsJsonAsync(ApiRoute("bookings"), newBooking);
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

        var response = await Client.PostAsJsonAsync(ApiRoute("bookings"), newBooking);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Post_ReturnsBadRequest_WhenOverlappingConfirmedBookingExists()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedBookingAsync(db);
        var checkin = DateTime.UtcNow.Date.AddDays(10);
        var checkout = checkin.AddDays(2);

        var firstBooking = new
        {
            listingId = data.listing.Id,
            guestId = data.guest.Id,
            checkinDate = checkin,
            checkoutDate = checkout,
            bookingSource = "airbnb",
            bookingStatus = "Confirmed",
            paymentStatus = "Paid",
            amountReceived = 100m,
            guestsPlanned = 1,
            guestsActual = 1,
            extraGuestCharge = 0m,
            notes = "first"
        };

        var firstResponse = await Client.PostAsJsonAsync(ApiRoute("bookings"), firstBooking);
        Assert.Equal(System.Net.HttpStatusCode.Created, firstResponse.StatusCode);

        var overlappingBooking = new
        {
            listingId = data.listing.Id,
            guestId = data.guest.Id,
            checkinDate = checkin.AddDays(1),
            checkoutDate = checkout.AddDays(1),
            bookingSource = "airbnb",
            bookingStatus = "Confirmed",
            paymentStatus = "Paid",
            amountReceived = 100m,
            guestsPlanned = 1,
            guestsActual = 1,
            extraGuestCharge = 0m,
            notes = "overlap"
        };

        var overlapResponse = await Client.PostAsJsonAsync(ApiRoute("bookings"), overlappingBooking);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, overlapResponse.StatusCode);
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
            commissionAmount = data.booking.CommissionAmount,
            paymentStatus = data.booking.PaymentStatus,
            notes = "updated"
        };
        var response = await Client.PutAsJsonAsync(ApiRoute($"bookings/{id}"), payload);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await db2.Bookings.FindAsync(id);
        Assert.Equal("updated", updated!.Notes);
    }

    [Fact]
    public async Task Put_CancelsAvailabilityBlock_WhenBookingCancelled()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedBookingAsync(db);
        var checkin = DateTime.UtcNow.Date.AddDays(15);
        var checkout = checkin.AddDays(2);

        var confirmedBooking = new
        {
            listingId = data.listing.Id,
            guestId = data.guest.Id,
            checkinDate = checkin,
            checkoutDate = checkout,
            bookingSource = "airbnb",
            bookingStatus = "Confirmed",
            paymentStatus = "Paid",
            amountReceived = 120m,
            guestsPlanned = 1,
            guestsActual = 1,
            extraGuestCharge = 0m,
            notes = "confirm"
        };

        var createResponse = await Client.PostAsJsonAsync(ApiRoute("bookings"), confirmedBooking);
        Assert.Equal(System.Net.HttpStatusCode.Created, createResponse.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = await db2.Bookings.OrderByDescending(b => b.Id).FirstAsync();
        var block = await db2.AvailabilityBlocks.SingleAsync(b => b.BookingId == booking.Id);
        Assert.Equal("Active", block.Status);
        Assert.Equal("Booking", block.BlockType);
        Assert.Equal("System", block.Source);
        Assert.True(block.CreatedAtUtc > DateTime.MinValue);
        Assert.True(block.UpdatedAtUtc > DateTime.MinValue);

        var updatePayload = new
        {
            id = booking.Id,
            listingId = booking.ListingId,
            guestId = booking.GuestId,
            checkinDate = booking.CheckinDate,
            checkoutDate = booking.CheckoutDate,
            bookingSource = booking.BookingSource,
            bookingStatus = "Cancelled",
            totalAmount = booking.TotalAmount,
            currency = booking.Currency,
            externalReservationId = booking.ExternalReservationId,
            confirmationSentAtUtc = booking.ConfirmationSentAtUtc,
            refundFreeUntilUtc = booking.RefundFreeUntilUtc,
            checkedInAtUtc = booking.CheckedInAtUtc,
            checkedOutAtUtc = booking.CheckedOutAtUtc,
            cancelledAtUtc = DateTime.UtcNow,
            paymentStatus = booking.PaymentStatus,
            amountReceived = booking.AmountReceived,
            bankAccountId = booking.BankAccountId,
            guestsPlanned = booking.GuestsPlanned,
            guestsActual = booking.GuestsActual,
            extraGuestCharge = booking.ExtraGuestCharge,
            commissionAmount = booking.CommissionAmount,
            notes = booking.Notes
        };

        var updateResponse = await Client.PutAsJsonAsync(ApiRoute($"bookings/{booking.Id}"), updatePayload);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, updateResponse.StatusCode);

        using var scope3 = Factory.Services.CreateScope();
        var db3 = scope3.ServiceProvider.GetRequiredService<AppDbContext>();
        var cancelledBlock = await db3.AvailabilityBlocks.SingleAsync(b => b.BookingId == booking.Id);
        Assert.Equal("Cancelled", cancelledBlock.Status);
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
            commissionAmount = data.booking.CommissionAmount,
            notes = "bad"
        };

        var response = await Client.PutAsJsonAsync(ApiRoute($"bookings/{id}"), payload);
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
            commissionAmount = 0m,
            notes = "n"
        };
        var response = await Client.PutAsJsonAsync(ApiRoute("bookings/2"), booking);
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

        var response = await Client.PostAsJsonAsync(ApiRoute("bookings"), request);

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
            CommissionAmount = data.booking.CommissionAmount,
            Notes = "updated",
            BankAccountId = data.booking.BankAccountId
        };

        var response = await Client.PutAsJsonAsync(ApiRoute($"bookings/{data.booking.Id}"), request);

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

        var response = await Client.PostAsJsonAsync(ApiRoute("bookings"), request);

        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesBooking()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedBookingAsync(db);
        var id = data.booking.Id;

        var response = await Client.DeleteAsync(ApiRoute($"bookings/{id}"));
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var exists = await db2.Bookings.AnyAsync(b => b.Id == id);
        Assert.False(exists);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        var response = await Client.DeleteAsync(ApiRoute("bookings/1"));
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_Cancel_UpdatesStatusAndAvailability()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedBookingAsync(db);

        var newBooking = new
        {
            listingId = data.listing.Id,
            guestId = data.guest.Id,
            checkinDate = DateTime.UtcNow.Date.AddDays(10),
            checkoutDate = DateTime.UtcNow.Date.AddDays(12),
            bookingSource = "airbnb",
            bookingStatus = "Confirmed",
            paymentStatus = "Paid",
            amountReceived = 100m,
            guestsPlanned = 1,
            guestsActual = 1,
            extraGuestCharge = 0m,
            notes = "confirm"
        };

        var createResponse = await Client.PostAsJsonAsync(ApiRoute("bookings"), newBooking);
        Assert.Equal(System.Net.HttpStatusCode.Created, createResponse.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = await db2.Bookings.OrderByDescending(b => b.Id).FirstAsync();

        var cancelResponse = await Client.PostAsync(ApiRoute($"bookings/{booking.Id}/cancel"), null);
        Assert.Equal(System.Net.HttpStatusCode.OK, cancelResponse.StatusCode);

        using var scope3 = Factory.Services.CreateScope();
        var db3 = scope3.ServiceProvider.GetRequiredService<AppDbContext>();
        var cancelled = await db3.Bookings.FindAsync(booking.Id);
        Assert.Equal("Cancelled", cancelled!.BookingStatus);
        Assert.NotNull(cancelled.CancelledAtUtc);

        var cancelledBlock = await db3.AvailabilityBlocks.SingleAsync(b => b.BookingId == booking.Id);
        Assert.Equal("Cancelled", cancelledBlock.Status);
    }

    [Fact]
    [Trait("Suite", "Smoke")]
    public async Task Post_CheckIn_UpdatesStatusAndTimestamp()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedBookingAsync(db);

        var newBooking = new
        {
            listingId = data.listing.Id,
            guestId = data.guest.Id,
            checkinDate = DateTime.UtcNow.Date.AddDays(5),
            checkoutDate = DateTime.UtcNow.Date.AddDays(6),
            bookingSource = "airbnb",
            bookingStatus = "Confirmed",
            paymentStatus = "Paid",
            amountReceived = 100m,
            guestsPlanned = 1,
            guestsActual = 1,
            extraGuestCharge = 0m,
            notes = "confirm"
        };

        var createResponse = await Client.PostAsJsonAsync(ApiRoute("bookings"), newBooking);
        Assert.Equal(System.Net.HttpStatusCode.Created, createResponse.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = await db2.Bookings.OrderByDescending(b => b.Id).FirstAsync();

        var checkInResponse = await Client.PostAsync(ApiRoute($"bookings/{booking.Id}/checkin"), null);
        Assert.Equal(System.Net.HttpStatusCode.OK, checkInResponse.StatusCode);

        using var scope3 = Factory.Services.CreateScope();
        var db3 = scope3.ServiceProvider.GetRequiredService<AppDbContext>();
        var checkedIn = await db3.Bookings.FindAsync(booking.Id);
        Assert.Equal("CheckedIn", checkedIn!.BookingStatus);
        Assert.NotNull(checkedIn.CheckedInAtUtc);
    }

    [Fact]
    [Trait("Suite", "Smoke")]
    public async Task Post_CheckOut_UpdatesStatusAndTimestamp()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedBookingAsync(db);

        var newBooking = new
        {
            listingId = data.listing.Id,
            guestId = data.guest.Id,
            checkinDate = DateTime.UtcNow.Date.AddDays(7),
            checkoutDate = DateTime.UtcNow.Date.AddDays(9),
            bookingSource = "airbnb",
            bookingStatus = "Confirmed",
            paymentStatus = "Paid",
            amountReceived = 100m,
            guestsPlanned = 1,
            guestsActual = 1,
            extraGuestCharge = 0m,
            notes = "confirm"
        };

        var createResponse = await Client.PostAsJsonAsync(ApiRoute("bookings"), newBooking);
        Assert.Equal(System.Net.HttpStatusCode.Created, createResponse.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = await db2.Bookings.OrderByDescending(b => b.Id).FirstAsync();

        var checkInResponse = await Client.PostAsync(ApiRoute($"bookings/{booking.Id}/checkin"), null);
        Assert.Equal(System.Net.HttpStatusCode.OK, checkInResponse.StatusCode);

        var checkOutResponse = await Client.PostAsync(ApiRoute($"bookings/{booking.Id}/checkout"), null);
        Assert.Equal(System.Net.HttpStatusCode.OK, checkOutResponse.StatusCode);

        using var scope3 = Factory.Services.CreateScope();
        var db3 = scope3.ServiceProvider.GetRequiredService<AppDbContext>();
        var checkedOut = await db3.Bookings.FindAsync(booking.Id);
        Assert.Equal("CheckedOut", checkedOut!.BookingStatus);
        Assert.NotNull(checkedOut.CheckedOutAtUtc);
    }
}
