using Atlas.Api.Controllers;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Atlas.Api.Models.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;

namespace Atlas.Api.Tests;

public class ReportsControllerTests
{
    [Fact]
    public async Task GetCalendarEarnings_SpansMultipleDays()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetCalendarEarnings_SpansMultipleDays))
            .Options;
        using var context = new AppDbContext(options);
        context.Bookings.Add(new Booking
        {
            ListingId = 1,
            GuestId = 1,
            CheckinDate = new DateTime(2025, 7, 5),
            CheckoutDate = new DateTime(2025, 7, 8),
            BookingSource = "airbnb",
            AmountReceived = 300,
            Notes = string.Empty,
            PaymentStatus = "Paid"
        });
        await context.SaveChangesAsync();

        var controller = new ReportsController(context, NullLogger<ReportsController>.Instance);
        var result = await controller.GetCalendarEarnings(1, "2025-07");
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dict = Assert.IsType<Dictionary<string, decimal>>(ok.Value);
        Assert.Equal(3, dict.Count);
        Assert.Equal(100, dict["2025-07-05"]);
        Assert.Equal(100, dict["2025-07-06"]);
        Assert.Equal(100, dict["2025-07-07"]);
    }

    [Fact]
    public async Task GetCalendarEarnings_PartialOverlap()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetCalendarEarnings_PartialOverlap))
            .Options;
        using var context = new AppDbContext(options);
        context.Bookings.Add(new Booking
        {
            ListingId = 1,
            GuestId = 1,
            CheckinDate = new DateTime(2025, 6, 30),
            CheckoutDate = new DateTime(2025, 7, 2),
            BookingSource = "airbnb",
            AmountReceived = 200,
            Notes = string.Empty,
            PaymentStatus = "Paid"
        });
        await context.SaveChangesAsync();

        var controller = new ReportsController(context, NullLogger<ReportsController>.Instance);
        var result = await controller.GetCalendarEarnings(1, "2025-07");
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dict = Assert.IsType<Dictionary<string, decimal>>(ok.Value);
        Assert.Single(dict);
        Assert.Equal(100, dict["2025-07-01"]);
    }

    [Fact]
    public async Task GetCalendarEarnings_WithinMonth()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetCalendarEarnings_WithinMonth))
            .Options;
        using var context = new AppDbContext(options);
        context.Bookings.Add(new Booking
        {
            ListingId = 1,
            GuestId = 1,
            CheckinDate = new DateTime(2025, 7, 10),
            CheckoutDate = new DateTime(2025, 7, 12),
            BookingSource = "airbnb",
            AmountReceived = 200,
            Notes = string.Empty,
            PaymentStatus = "Paid"
        });
        await context.SaveChangesAsync();

        var controller = new ReportsController(context, NullLogger<ReportsController>.Instance);
        var result = await controller.GetCalendarEarnings(1, "2025-07");
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dict = Assert.IsType<Dictionary<string, decimal>>(ok.Value);
        Assert.Equal(2, dict.Count);
        Assert.Equal(100, dict["2025-07-10"]);
        Assert.Equal(100, dict["2025-07-11"]);
    }

    [Fact]
    public async Task GetBankAccountEarnings_IncludesValidBookings()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetBankAccountEarnings_IncludesValidBookings))
            .Options;
        using var context = new AppDbContext(options);
        var account = new BankAccount { Id = 1, BankName = "Bank", AccountNumber = "0000861", IFSC = "IFSC", AccountType = "Savings" };
        context.BankAccounts.Add(account);
        context.Bookings.Add(new Booking
        {
            ListingId = 1,
            GuestId = 1,
            BankAccountId = 1,
            CheckinDate = new DateTime(2025, 4, 1),
            CheckoutDate = new DateTime(2025, 4, 2),
            BookingSource = "airbnb",
            AmountReceived = 100,
            Notes = string.Empty,
            PaymentStatus = "Paid"
        });
        await context.SaveChangesAsync();

        var controller = new ReportsController(context, NullLogger<ReportsController>.Instance);
        var result = await controller.GetBankAccountEarnings();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<BankAccountEarnings>>(ok.Value).ToList();
        Assert.Single(list);
        Assert.Equal(100, list[0].AmountReceived);
        Assert.Equal("Bank - 0861", list[0].AccountDisplay);
    }

    [Fact]
    public async Task GetBankAccountEarnings_ExcludesOutsideRange()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetBankAccountEarnings_ExcludesOutsideRange))
            .Options;
        using var context = new AppDbContext(options);
        context.BankAccounts.Add(new BankAccount { Id = 1, BankName = "Bank", AccountNumber = "123456", IFSC = "I", AccountType = "S" });
        context.Bookings.Add(new Booking
        {
            ListingId = 1,
            GuestId = 1,
            BankAccountId = 1,
            CheckinDate = new DateTime(2026, 4, 1),
            CheckoutDate = new DateTime(2026, 4, 2),
            BookingSource = "airbnb",
            AmountReceived = 100,
            Notes = string.Empty,
            PaymentStatus = "Paid"
        });
        await context.SaveChangesAsync();

        var controller = new ReportsController(context, NullLogger<ReportsController>.Instance);
        var result = await controller.GetBankAccountEarnings();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<BankAccountEarnings>>(ok.Value).ToList();
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetBankAccountEarnings_ReturnsEmpty_WhenNoBookings()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetBankAccountEarnings_ReturnsEmpty_WhenNoBookings))
            .Options;
        using var context = new AppDbContext(options);
        var controller = new ReportsController(context, NullLogger<ReportsController>.Instance);
        var result = await controller.GetBankAccountEarnings();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<BankAccountEarnings>>(ok.Value);
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetBankAccountEarnings_AggregatesByAccount()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetBankAccountEarnings_AggregatesByAccount))
            .Options;
        using var context = new AppDbContext(options);
        context.BankAccounts.Add(new BankAccount { Id = 1, BankName = "Bank", AccountNumber = "99993290", IFSC = "I", AccountType = "S" });
        context.Bookings.AddRange(
            new Booking
            {
                ListingId = 1,
                GuestId = 1,
                BankAccountId = 1,
                CheckinDate = new DateTime(2025, 5, 1),
                CheckoutDate = new DateTime(2025, 5, 2),
                BookingSource = "airbnb",
                AmountReceived = 200,
                Notes = string.Empty,
                PaymentStatus = "Paid"
            },
            new Booking
            {
                ListingId = 1,
                GuestId = 1,
                BankAccountId = 1,
                CheckinDate = new DateTime(2025, 5, 3),
                CheckoutDate = new DateTime(2025, 5, 4),
                BookingSource = "airbnb",
                AmountReceived = 300,
                Notes = string.Empty,
                PaymentStatus = "Paid"
            });
        await context.SaveChangesAsync();

        var controller = new ReportsController(context, NullLogger<ReportsController>.Instance);
        var result = await controller.GetBankAccountEarnings();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<BankAccountEarnings>>(ok.Value).ToList();
        Assert.Single(list);
        Assert.Equal(500, list[0].AmountReceived);
    }
}
