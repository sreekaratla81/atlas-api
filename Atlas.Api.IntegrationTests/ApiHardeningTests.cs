using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Atlas.Api.IntegrationTests;

[Trait("Suite", "FD003")]
public class ApiHardeningTests : IntegrationTestBase
{
    public ApiHardeningTests(SqlServerTestDatabase database) : base(database) {}

    private async Task<(Listing listing, Guest guest)> SeedCoreAsync(AppDbContext db)
    {
        var property = new Property
        {
            Name = "Test Property", Address = "123 St", Type = "House",
            OwnerName = "Owner", ContactPhone = "555-0000", CommissionPercent = 10, Status = "Active"
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var listing = new Listing
        {
            PropertyId = property.Id, Property = property, Name = "Test Listing",
            Floor = 1, Type = "Room", Status = "Available",
            WifiName = "wifi", WifiPassword = "pass", MaxGuests = 2
        };
        db.Listings.Add(listing);

        var guest = new Guest { Name = "Guest", Phone = "123456", Email = "guest@test.com", IdProofUrl = "N/A" };
        db.Guests.Add(guest);
        await db.SaveChangesAsync();

        return (listing, guest);
    }

    [Fact]
    public async Task ExceptionFilter_Returns404Json_WhenBookingNotFound()
    {
        var response = await Client.GetAsync(ApiRoute("bookings/999999"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExceptionFilter_Returns400Json_WhenPaymentMissingMethod()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (listing, guest) = await SeedCoreAsync(db);

        var booking = new Booking
        {
            ListingId = listing.Id, GuestId = guest.Id, BookingSource = "direct",
            PaymentStatus = "Pending", CheckinDate = DateTime.UtcNow.Date,
            CheckoutDate = DateTime.UtcNow.Date.AddDays(1), AmountReceived = 100,
            GuestsPlanned = 1, GuestsActual = 1, ExtraGuestCharge = 0, CommissionAmount = 0
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var payload = new { BookingId = booking.Id, Amount = 100, Method = "", Type = "credit" };
        var response = await Client.PostAsJsonAsync(ApiControllerRoute("payments"), payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("error", body);
    }

    [Fact]
    public async Task Delete_Booking_CleansUpAvailabilityBlocks()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (listing, guest) = await SeedCoreAsync(db);

        var booking = new Booking
        {
            ListingId = listing.Id, GuestId = guest.Id, BookingSource = "direct",
            BookingStatus = "Confirmed", PaymentStatus = "Paid",
            CheckinDate = DateTime.UtcNow.Date, CheckoutDate = DateTime.UtcNow.Date.AddDays(2),
            AmountReceived = 200, GuestsPlanned = 1, GuestsActual = 1,
            ExtraGuestCharge = 0, CommissionAmount = 0
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var block = new AvailabilityBlock
        {
            ListingId = listing.Id, BookingId = booking.Id,
            StartDate = booking.CheckinDate, EndDate = booking.CheckoutDate,
            BlockType = "Booking", Source = "System", Status = "Active",
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.AvailabilityBlocks.Add(block);
        await db.SaveChangesAsync();

        var response = await Client.DeleteAsync(ApiRoute($"bookings/{booking.Id}"));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var checkScope = Factory.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orphanBlocks = await checkDb.AvailabilityBlocks.Where(b => b.BookingId == booking.Id).CountAsync();
        Assert.Equal(0, orphanBlocks);
    }

    [Fact]
    public async Task Update_ToCancelled_EmitsBookingCancelledOutbox()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (listing, guest) = await SeedCoreAsync(db);

        var booking = new Booking
        {
            ListingId = listing.Id, GuestId = guest.Id, BookingSource = "direct",
            BookingStatus = "Confirmed", PaymentStatus = "Paid",
            CheckinDate = DateTime.UtcNow.Date.AddDays(5),
            CheckoutDate = DateTime.UtcNow.Date.AddDays(7),
            AmountReceived = 300, GuestsPlanned = 2, GuestsActual = 2,
            ExtraGuestCharge = 0, CommissionAmount = 0
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var update = new
        {
            Id = booking.Id, ListingId = listing.Id, GuestId = guest.Id,
            BookingSource = "direct", BookingStatus = "Cancelled", PaymentStatus = "Paid",
            CheckinDate = booking.CheckinDate, CheckoutDate = booking.CheckoutDate,
            AmountReceived = 300, GuestsPlanned = 2, GuestsActual = 2,
            ExtraGuestCharge = 0, CommissionAmount = 0
        };

        var response = await Client.PutAsJsonAsync(ApiRoute($"bookings/{booking.Id}"), update);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var checkScope = Factory.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var outbox = await checkDb.OutboxMessages
            .Where(o => o.EventType == "booking.cancelled" && o.EntityId == booking.Id.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(outbox);
    }

    [Fact]
    public async Task Payment_Create_Returns404_WhenBookingDoesNotExist()
    {
        var payload = new { BookingId = 999999, Amount = 100, Method = "UPI", Type = "credit" };
        var response = await Client.PostAsJsonAsync(ApiControllerRoute("payments"), payload);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Guest_Create_Returns400_WhenNameMissing()
    {
        var payload = new { Name = "", Phone = "123", Email = "a@b.com" };
        var response = await Client.PostAsJsonAsync(ApiRoute("guests"), payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
