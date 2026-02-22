using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Atlas.Api.IntegrationTests;

[Trait("Suite", "FD004")]
public class FD004SecurityAndDtoTests : IntegrationTestBase
{
    public FD004SecurityAndDtoTests(SqlServerTestDatabase database) : base(database) {}

    private async Task<(Listing listing, Guest guest)> SeedCoreAsync(AppDbContext db)
    {
        var property = new Property
        {
            Name = "FD004 Property", Address = "Test St", Type = "House",
            OwnerName = "Owner", ContactPhone = "555-0000", CommissionPercent = 10, Status = "Active"
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var listing = new Listing
        {
            PropertyId = property.Id, Property = property, Name = "FD004 Listing",
            Floor = 1, Type = "Room", Status = "Available",
            WifiName = "wifi", WifiPassword = "pass", MaxGuests = 2
        };
        db.Listings.Add(listing);

        var guest = new Guest { Name = "FD004 Guest", Phone = "1234567890", Email = "fd004@test.com", IdProofUrl = "N/A" };
        db.Guests.Add(guest);
        await db.SaveChangesAsync();

        return (listing, guest);
    }

    [Fact]
    public async Task Guest_Create_ReturnsDto_WithoutTenantId()
    {
        var payload = new { Name = "New Guest", Phone = "9876543210", Email = "new@test.com" };
        var response = await Client.PostAsJsonAsync(ApiRoute("guests"), payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("name", out var name));
        Assert.Equal("New Guest", name.GetString());
        Assert.True(doc.RootElement.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task Property_Create_ReturnsDto_WithoutTenantId()
    {
        var payload = new
        {
            Name = "New Prop", Address = "1 Main St", Type = "Villa",
            OwnerName = "Owner1", ContactPhone = "000111"
        };
        var response = await Client.PostAsJsonAsync(ApiRoute("properties"), payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("name", out var name));
        Assert.Equal("New Prop", name.GetString());
        Assert.False(doc.RootElement.TryGetProperty("tenantId", out _));
    }

    [Fact]
    public async Task Property_Create_Returns400_WhenNameMissing()
    {
        var payload = new { Name = "", Address = "1 Main St", Type = "Villa", OwnerName = "O", ContactPhone = "0" };
        var response = await Client.PostAsJsonAsync(ApiRoute("properties"), payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Payment_Create_Returns404_WhenBookingMissing()
    {
        var payload = new { BookingId = 999999, Amount = 100, Method = "UPI", Type = "credit" };
        var response = await Client.PostAsJsonAsync(ApiControllerRoute("payments"), payload);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Payment_Response_ExcludesRazorpaySignature()
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

        var payload = new { BookingId = booking.Id, Amount = 100, Method = "UPI", Type = "credit" };
        var response = await Client.PostAsJsonAsync(ApiControllerRoute("payments"), payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.False(doc.RootElement.TryGetProperty("razorpaySignature", out _),
            "PaymentResponseDto must not expose razorpaySignature");
    }

    [Fact]
    public async Task Incident_Create_WithDto_ReturnsIncidentResponse()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (listing, _) = await SeedCoreAsync(db);

        var payload = new
        {
            ListingId = listing.Id, Description = "broken AC", ActionTaken = "called repair",
            Status = "open", CreatedBy = "admin"
        };
        var response = await Client.PostAsJsonAsync(ApiControllerRoute("incidents"), payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("description", out var desc));
        Assert.Equal("broken AC", desc.GetString());
    }

    [Fact]
    public async Task CorrelationId_ReturnedInResponseHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoute("ops/outbox"));
        request.Headers.Add("X-Correlation-Id", "test-corr-123");

        var response = await Client.SendAsync(request);
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        var values = response.Headers.GetValues("X-Correlation-Id").ToList();
        Assert.Contains("test-corr-123", values);
    }

    [Fact]
    public async Task CorrelationId_GeneratedWhenNotProvided()
    {
        var response = await Client.GetAsync(ApiRoute("ops/outbox"));
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        var value = response.Headers.GetValues("X-Correlation-Id").First();
        Assert.False(string.IsNullOrWhiteSpace(value));
    }

    [Fact]
    public async Task Checkin_EmitsStayCheckedInOutbox()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (listing, guest) = await SeedCoreAsync(db);

        var booking = new Booking
        {
            ListingId = listing.Id, GuestId = guest.Id, BookingSource = "direct",
            BookingStatus = "Confirmed", PaymentStatus = "Paid",
            CheckinDate = DateTime.UtcNow.Date,
            CheckoutDate = DateTime.UtcNow.Date.AddDays(2),
            AmountReceived = 200, GuestsPlanned = 1, GuestsActual = 1,
            ExtraGuestCharge = 0, CommissionAmount = 0
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var response = await Client.PostAsync(ApiRoute($"bookings/{booking.Id}/checkin"), null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var checkScope = Factory.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var outbox = await checkDb.OutboxMessages
            .Where(o => o.EventType == "stay.checked_in" && o.EntityId == booking.Id.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(outbox);
    }

    [Fact]
    public async Task Checkout_EmitsStayCheckedOutOutbox()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (listing, guest) = await SeedCoreAsync(db);

        var booking = new Booking
        {
            ListingId = listing.Id, GuestId = guest.Id, BookingSource = "direct",
            BookingStatus = "CheckedIn", PaymentStatus = "Paid",
            CheckinDate = DateTime.UtcNow.Date.AddDays(-1),
            CheckoutDate = DateTime.UtcNow.Date.AddDays(1),
            AmountReceived = 200, GuestsPlanned = 1, GuestsActual = 1,
            ExtraGuestCharge = 0, CommissionAmount = 0,
            CheckedInAtUtc = DateTime.UtcNow.AddDays(-1)
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var response = await Client.PostAsync(ApiRoute($"bookings/{booking.Id}/checkout"), null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var checkScope = Factory.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var outbox = await checkDb.OutboxMessages
            .Where(o => o.EventType == "stay.checked_out" && o.EntityId == booking.Id.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(outbox);
    }
}
