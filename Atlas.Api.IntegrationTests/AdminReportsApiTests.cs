using Atlas.Api.Data;
using Atlas.Api.Models;
using Atlas.Api.Models.Reports;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

[Trait("Suite", "Contract")]
public class AdminReportsApiTests : IntegrationTestBase
{
    public AdminReportsApiTests(CustomWebApplicationFactory factory) : base(factory) { }

    private static async Task SeedReportingDataAsync(AppDbContext db)
    {
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        var guest = await DataSeeder.SeedGuestAsync(db);
        var booking = await DataSeeder.SeedBookingAsync(db, property, listing, guest);
        await DataSeeder.SeedPaymentAsync(db, booking);
    }

    [Fact]
    public async Task GetBookings_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedReportingDataAsync(db);

        var response = await Client.GetAsync("/api/admin/reports/bookings");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var data = await response.Content.ReadFromJsonAsync<List<BookingInfo>>();
        Assert.NotEmpty(data!);
    }

    [Fact]
    public async Task GetListings_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await DataSeeder.SeedPropertyAsync(db);
        await DataSeeder.SeedListingAsync(db, property);

        var response = await Client.GetAsync("/api/admin/reports/listings");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetPayouts_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedReportingDataAsync(db);

        var response = await Client.GetAsync("/api/admin/reports/payouts");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMonthlyEarnings_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedReportingDataAsync(db);

        var response = await Client.GetAsync("/api/admin/reports/earnings/monthly");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var data = await response.Content.ReadFromJsonAsync<List<MonthlyEarningsSummary>>();
        Assert.Equal(12, data!.Count);
    }

    [Fact]
    public async Task PostMonthlyEarnings_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedReportingDataAsync(db);

        var filter = new ReportFilter { StartDate = DateTime.UtcNow.AddMonths(-1), EndDate = DateTime.UtcNow };
        var response = await Client.PostAsJsonAsync("/api/admin/reports/earnings/monthly", filter);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDailyPayout_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedReportingDataAsync(db);

        var response = await Client.GetAsync("/api/admin/reports/payouts/daily");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBookingSource_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedReportingDataAsync(db);

        var response = await Client.GetAsync("/api/admin/reports/bookings/source");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCalendarBookings_ReturnsOk()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedReportingDataAsync(db);

        var response = await Client.GetAsync("/api/admin/reports/bookings/calendar");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
