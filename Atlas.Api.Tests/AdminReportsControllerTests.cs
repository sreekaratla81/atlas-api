using Atlas.Api.Controllers;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Atlas.Api.Models.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;

public class AdminReportsControllerTests
{
    [Fact]
    public async Task GetMonthlyEarnings_ComputesGrossNetAndFees()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "MonthlyEarningsTest")
            .Options;

        using var context = new AppDbContext(options);
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1).AddMonths(-11);

        context.Bookings.AddRange(
            new Booking { ListingId = 1, GuestId = 1, CheckinDate = start.AddDays(1), BookingSource = "airbnb", PaymentStatus = "Paid", AmountReceived = 100, Notes = string.Empty },
            new Booking { ListingId = 1, GuestId = 1, CheckinDate = start.AddMonths(1).AddDays(2), BookingSource = "booking.com", PaymentStatus = "Paid", AmountReceived = 200, Notes = string.Empty },
            new Booking { ListingId = 1, GuestId = 1, CheckinDate = start.AddMonths(1).AddDays(3), BookingSource = "agoda", PaymentStatus = "Paid", AmountReceived = 300, Notes = string.Empty }
        );
        await context.SaveChangesAsync();

        var controller = new AdminReportsController(context, NullLogger<AdminReportsController>.Instance);

        var result = await controller.GetMonthlyEarnings();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var summaries = Assert.IsAssignableFrom<IEnumerable<MonthlyEarningsSummary>>(ok.Value);
        var list = summaries.ToList();

        var key0 = start.ToString("yyyy-MM");
        var key1 = start.AddMonths(1).ToString("yyyy-MM");

        var first = list.Single(x => x.Month == key0);
        Assert.Equal(100, first.TotalNet);
        Assert.Equal(100 * 0.16m, first.TotalFees);
        Assert.Equal(100 + 100 * 0.16m, first.TotalGross);

        var second = list.Single(x => x.Month == key1);
        var net = 200 + 300;
        var fees = 200 * 0.15m + 300 * 0.18m;
        Assert.Equal(net, second.TotalNet);
        Assert.Equal(fees, second.TotalFees);
        Assert.Equal(net + fees, second.TotalGross);
    }
}
