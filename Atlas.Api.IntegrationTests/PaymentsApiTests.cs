using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

[Trait("Suite", "Contract")]
public class PaymentsApiTests : IntegrationTestBase
{
    public PaymentsApiTests(CustomWebApplicationFactory factory) : base(factory) {}

    private async Task<Booking> SeedBookingAsync(AppDbContext db)
    {
        var property = new Property
        {
            Name = "Prop",
            Address = "Addr",
            Type = "House",
            OwnerName = "Owner",
            ContactPhone = "123",
            CommissionPercent = 10,
            Status = "Active"
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();
        var listing = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Listing",
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
            Phone = "1",
            Email = "g@example.com",
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
        return booking;
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = await SeedBookingAsync(db);
        db.Payments.Add(new Payment
        {
            BookingId = booking.Id,
            Amount = 50,
            Method = "cash",
            Type = "partial",
            ReceivedOn = DateTime.UtcNow,
            Note = "first"
        });
        await db.SaveChangesAsync();

        var response = await Client.GetAsync(ApiControllerRoute("payments"));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = await SeedBookingAsync(db);
        var payment = await DataSeeder.SeedPaymentAsync(db, booking);

        var response = await Client.GetAsync(ApiControllerRoute($"payments/{payment.Id}"));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenMissing()
    {
        var response = await Client.GetAsync(ApiControllerRoute("payments/1"));
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_CreatesPayment()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = await SeedBookingAsync(db);
        var payment = new PaymentCreateDto
        {
            BookingId = booking.Id,
            Amount = 50,
            Method = "cash",
            Type = "partial",
            ReceivedOn = DateTime.UtcNow,
            Note = "first"
        };
        var response = await Client.PostAsJsonAsync(ApiControllerRoute("payments"), payment);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db2.Payments.CountAsync());
    }

    [Fact]
    public async Task Put_UpdatesPayment()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = await SeedBookingAsync(db);
        var payment = new Payment
        {
            BookingId = booking.Id,
            Amount = 50,
            Method = "cash",
            Type = "partial",
            ReceivedOn = DateTime.UtcNow,
            Note = "first"
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var update = new PaymentUpdateDto
        {
            BookingId = booking.Id,
            Amount = payment.Amount,
            Method = payment.Method,
            Type = payment.Type,
            ReceivedOn = payment.ReceivedOn,
            Note = "updated"
        };
        var response = await Client.PutAsJsonAsync(ApiControllerRoute($"payments/{payment.Id}"), update);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await db2.Payments.FindAsync(payment.Id);
        Assert.Equal("updated", updated!.Note);
    }

    [Fact]
    public async Task Put_ReturnsNotFound_WhenMissing()
    {
        var payment = new PaymentUpdateDto { BookingId = 1, Amount = 1, Method = "c", Type = "t", ReceivedOn = DateTime.UtcNow, Note = "n" };
        var response = await Client.PutAsJsonAsync(ApiControllerRoute("payments/2"), payment);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesPayment()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = await SeedBookingAsync(db);
        var payment = new Payment
        {
            BookingId = booking.Id,
            Amount = 50,
            Method = "cash",
            Type = "partial",
            ReceivedOn = DateTime.UtcNow,
            Note = "first"
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        var id = payment.Id;

        var response = await Client.DeleteAsync(ApiControllerRoute($"payments/{id}"));
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var exists = await db2.Payments.AnyAsync(p => p.Id == id);
        Assert.False(exists);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        var response = await Client.DeleteAsync(ApiControllerRoute("payments/1"));
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
