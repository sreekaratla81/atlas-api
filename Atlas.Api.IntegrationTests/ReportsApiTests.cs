using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.Extensions.DependencyInjection;
using Atlas.Api.Models.Reports;

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

    private static async Task SeedSameDayBookingAsync(AppDbContext db)
    {
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        var guest = await DataSeeder.SeedGuestAsync(db);
        db.Bookings.Add(new Booking
        {
            ListingId = listing.Id,
            GuestId = guest.Id,
            CheckinDate = new DateTime(2025, 6, 18),
            CheckoutDate = new DateTime(2025, 6, 18),
            BookingSource = "direct",
            AmountReceived = 2650.88m,
            Notes = string.Empty,
            PaymentStatus = "Paid"
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedBankAccountDataAsync(AppDbContext db)
    {
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        var guest = await DataSeeder.SeedGuestAsync(db);
        var account = await DataSeeder.SeedBankAccountAsync(db);
        db.Bookings.Add(new Booking
        {
            ListingId = listing.Id,
            GuestId = guest.Id,
            BankAccountId = account.Id,
            CheckinDate = new DateTime(2025, 5, 10),
            CheckoutDate = new DateTime(2025, 5, 12),
            BookingSource = "airbnb",
            AmountReceived = 400,
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

        var list = await response.Content.ReadFromJsonAsync<List<DailySourceEarnings>>();
        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
    }

    [Fact]
    public async Task GetCalendarEarnings_IncludesSameDayBooking()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedSameDayBookingAsync(db);

        var response = await Client.GetAsync("/api/reports/calendar-earnings?listingId=1&month=2025-06");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var list = await response.Content.ReadFromJsonAsync<List<DailySourceEarnings>>();
        Assert.NotNull(list);
        Assert.Single(list!);
        var entry = list![0];
        Assert.Equal("2025-06-18", entry.Date);
        Assert.Equal("direct", entry.Source);
        Assert.Equal(2650.88m, entry.Amount);
    }

    [Fact]
    public async Task GetBankAccountEarnings_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedBankAccountDataAsync(db);

        var response = await Client.GetAsync("/api/reports/bank-account-earnings");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var data = await response.Content.ReadFromJsonAsync<List<BankAccountEarnings>>();
        Assert.NotNull(data);
        Assert.Single(data!);
        Assert.Equal(400, data![0].AmountReceived);
    }

    [Fact]
    public async Task GetBankAccountEarnings_ReturnsZeroForAccountWithoutBookings()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DataSeeder.SeedBankAccountAsync(db);

        var response = await Client.GetAsync("/api/reports/bank-account-earnings");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var data = await response.Content.ReadFromJsonAsync<List<BankAccountEarnings>>();
        Assert.NotNull(data);
        Assert.Single(data!);
        Assert.Equal(0, data![0].AmountReceived);
    }
}
