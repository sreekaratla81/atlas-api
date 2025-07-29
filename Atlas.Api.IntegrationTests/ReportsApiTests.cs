using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

public class ReportsApiTests : IntegrationTestBase
{
    public ReportsApiTests(CustomWebApplicationFactory factory) : base(factory) { }

    private static async Task SeedDataAsync(AppDbContext db)
    {
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        var guest = await DataSeeder.SeedGuestAsync(db);
        db.Bookings.Add(new Booking
        {
            ListingId = listing.Id,
            GuestId = guest.Id,
            CheckinDate = new DateTime(2025, 7, 1),
            CheckoutDate = new DateTime(2025, 7, 3),
            BookingSource = "airbnb",
            AmountReceived = 200,
            Notes = string.Empty,
            PaymentStatus = "Paid"
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetCalendarEarnings_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedDataAsync(db);

        var response = await Client.GetAsync("/api/reports/calendar-earnings?listingId=1&month=2025-07");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var dict = await response.Content.ReadFromJsonAsync<Dictionary<string, decimal>>();
        Assert.NotNull(dict);
        Assert.Equal(2, dict!.Count);
    }
}
