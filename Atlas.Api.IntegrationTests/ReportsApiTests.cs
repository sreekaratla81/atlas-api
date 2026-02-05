using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.Extensions.DependencyInjection;
using Atlas.Api.Models.Reports;

namespace Atlas.Api.IntegrationTests;

[Trait("Suite", "Contract")]
public class ReportsApiTests : IntegrationTestBase
{
    public ReportsApiTests(CustomWebApplicationFactory factory) : base(factory) { }

    private static async Task<int> SeedDataAsync(AppDbContext db)
    {
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        var guest = await DataSeeder.SeedGuestAsync(db);
        db.Bookings.Add(new Booking
        {
            ListingId = listing.Id,
            GuestId = guest.Id,
            CheckinDate = new DateTime(2025, 6, 30),
            CheckoutDate = new DateTime(2025, 7, 2),
            BookingSource = "airbnb",
            AmountReceived = 200,
            Notes = string.Empty,
            PaymentStatus = "Paid"
        });
        await db.SaveChangesAsync();
        return listing.Id;
    }

    private static async Task<int> SeedSameDayBookingAsync(AppDbContext db)
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
        return listing.Id;
    }

    private static async Task<int> SeedNullSourceBookingAsync(AppDbContext db)
    {
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        var guest = await DataSeeder.SeedGuestAsync(db);
        db.Bookings.Add(new Booking
        {
            ListingId = listing.Id,
            GuestId = guest.Id,
            CheckinDate = new DateTime(2025, 7, 10),
            CheckoutDate = new DateTime(2025, 7, 11),
            BookingSource = null,
            AmountReceived = 120,
            Notes = string.Empty,
            PaymentStatus = "Paid"
        });
        await db.SaveChangesAsync();
        return listing.Id;
    }

    private static async Task<BankAccount> SeedBankAccountDataAsync(AppDbContext db)
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
        return account;
    }

    [Fact]
    public async Task GetCalendarEarnings_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var listingId = await SeedDataAsync(db);

        var response = await Client.GetAsync(ApiRoute($"reports/calendar-earnings?listingId={listingId}&month=2025-07"));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var list = await response.Content.ReadFromJsonAsync<List<CalendarEarningEntry>>();
        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
        Assert.All(list!, e => Assert.Equal(new DateTime(2025, 6, 30), e.Earnings[0].CheckinDate));
        Assert.False(string.IsNullOrWhiteSpace(list![0].Earnings[0].GuestName));
    }

    [Fact]
    public async Task GetCalendarEarnings_IncludesSameDayBooking()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var listingId = await SeedSameDayBookingAsync(db);

        var response = await Client.GetAsync(ApiRoute($"reports/calendar-earnings?listingId={listingId}&month=2025-06"));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var list = await response.Content.ReadFromJsonAsync<List<CalendarEarningEntry>>();
        Assert.NotNull(list);
        Assert.Single(list!);
        var entry = list![0];
        Assert.Equal(new DateTime(2025, 6, 18), entry.Date);
        Assert.Single(entry.Earnings);
        var earning = entry.Earnings[0];
        Assert.Equal("direct", earning.Source);
        Assert.Equal(2650.88m, earning.Amount);
        Assert.Equal("Guest", earning.GuestName);
        Assert.Equal(new DateTime(2025, 6, 18), earning.CheckinDate);
        Assert.Equal(2650.88m, entry.Total);
    }

    [Fact]
    public async Task GetCalendarEarnings_ReturnsUnknownSource_WhenSourceIsNull()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var listingId = await SeedNullSourceBookingAsync(db);

        var response = await Client.GetAsync(ApiRoute($"reports/calendar-earnings?listingId={listingId}&month=2025-07"));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var list = await response.Content.ReadFromJsonAsync<List<CalendarEarningEntry>>();
        Assert.NotNull(list);
        Assert.Single(list!);
        var entry = list![0];
        Assert.Single(entry.Earnings);
        Assert.Equal("Unknown", entry.Earnings[0].Source);
    }

    [Fact]
    public async Task GetCalendarEarnings_AllowsMultipleEntriesSameSource()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        var guest = await DataSeeder.SeedGuestAsync(db);
        db.Bookings.AddRange(
            new Booking
            {
                ListingId = listing.Id,
                GuestId = guest.Id,
                CheckinDate = new DateTime(2025, 7, 15),
                CheckoutDate = new DateTime(2025, 7, 15),
                BookingSource = "walk-in",
                AmountReceived = 100,
                Notes = string.Empty,
                PaymentStatus = "Paid"
            },
            new Booking
            {
                ListingId = listing.Id,
                GuestId = guest.Id,
                CheckinDate = new DateTime(2025, 7, 15),
                CheckoutDate = new DateTime(2025, 7, 15),
                BookingSource = "walk-in",
                AmountReceived = 150,
                Notes = string.Empty,
                PaymentStatus = "Paid"
            });
        await db.SaveChangesAsync();

        var response = await Client.GetAsync(ApiRoute($"reports/calendar-earnings?listingId={listing.Id}&month=2025-07"));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var list = await response.Content.ReadFromJsonAsync<List<CalendarEarningEntry>>();
        Assert.NotNull(list);
        Assert.Single(list!);
        var entry = list![0];
        Assert.Equal(2, entry.Earnings.Count);
        Assert.Equal(250, entry.Total);
        Assert.All(entry.Earnings, e => Assert.Equal("Guest", e.GuestName));
        Assert.All(entry.Earnings, e => Assert.Equal(new DateTime(2025, 7, 15), e.CheckinDate));
    }

    [Fact]
    public async Task GetBankAccountEarnings_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await SeedBankAccountDataAsync(db);

        var response = await Client.GetAsync(ApiRoute("reports/bank-account-earnings"));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var data = await response.Content.ReadFromJsonAsync<List<BankAccountEarnings>>();
        Assert.NotNull(data);
        var expectedDisplay = $"{account.BankName} - {account.AccountNumber}";
        var accountEntry = Assert.Single(data!.Where(entry => entry.AccountDisplay == expectedDisplay && entry.Bank == account.BankName));
        Assert.Equal(400, accountEntry.AmountReceived);
    }

    [Fact]
    public async Task GetBankAccountEarnings_ReturnsZeroForAccountWithoutBookings()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await DataSeeder.SeedBankAccountAsync(db);

        var response = await Client.GetAsync(ApiRoute("reports/bank-account-earnings"));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var data = await response.Content.ReadFromJsonAsync<List<BankAccountEarnings>>();
        Assert.NotNull(data);
        var expectedDisplay = $"{account.BankName} - {account.AccountNumber}";
        var accountEntry = Assert.Single(data!.Where(entry => entry.AccountDisplay == expectedDisplay && entry.Bank == account.BankName));
        Assert.Equal(0, accountEntry.AmountReceived);
    }
}
