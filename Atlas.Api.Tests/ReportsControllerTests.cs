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
    public async Task GetCalendarEarnings_SameDayBooking()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetCalendarEarnings_SameDayBooking))
            .Options;
        using var context = new AppDbContext(options);
        context.Bookings.Add(new Booking
        {
            ListingId = 1,
            GuestId = 1,
            CheckinDate = new DateTime(2025, 6, 18),
            CheckoutDate = new DateTime(2025, 6, 18),
            BookingSource = "direct",
            AmountReceived = 2650.88m,
            Notes = string.Empty,
            PaymentStatus = "Paid"
        });
        await context.SaveChangesAsync();

        var controller = new ReportsController(context, NullLogger<ReportsController>.Instance);
        var result = await controller.GetCalendarEarnings(1, "2025-06");
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<CalendarEarningEntry>>(ok.Value).ToList();
        Assert.Single(list);
        var entry = list.Single();
        Assert.Equal(new DateTime(2025, 6, 18), entry.Date);
        Assert.Single(entry.Earnings);
        var detail = entry.Earnings.Single();
        Assert.Equal("direct", detail.Source);
        Assert.Equal(2650.88m, detail.Amount);
        Assert.Equal(1, detail.BookingId);
        Assert.Equal("Unknown Guest", detail.GuestName);
        Assert.Equal(2650.88m, entry.Total);
    }

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
        var list = Assert.IsAssignableFrom<IEnumerable<CalendarEarningEntry>>(ok.Value).ToList();
        Assert.Equal(3, list.Count);
        Assert.All(list, i => Assert.Equal(1, i.Earnings.Count));
        Assert.All(list, i => Assert.Equal("airbnb", i.Earnings[0].Source));
        Assert.All(list, i => Assert.Equal(1, i.Earnings[0].BookingId));
        Assert.All(list, i => Assert.Equal("Unknown Guest", i.Earnings[0].GuestName));
        Assert.Contains(list, i => i.Date == new DateTime(2025, 7, 5) && i.Earnings[0].Amount == 100);
        Assert.Contains(list, i => i.Date == new DateTime(2025, 7, 6) && i.Earnings[0].Amount == 100);
        Assert.Contains(list, i => i.Date == new DateTime(2025, 7, 7) && i.Earnings[0].Amount == 100);
    }

    [Fact]
    public async Task GetCalendarEarnings_MultipleEntriesSameSource()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetCalendarEarnings_MultipleEntriesSameSource))
            .Options;
        using var context = new AppDbContext(options);
        context.Bookings.AddRange(
            new Booking
            {
                ListingId = 1,
                GuestId = 1,
                CheckinDate = new DateTime(2025, 7, 15),
                CheckoutDate = new DateTime(2025, 7, 15),
                BookingSource = "walk-in",
                AmountReceived = 100,
                Notes = string.Empty,
                PaymentStatus = "Paid"
            },
            new Booking
            {
                ListingId = 1,
                GuestId = 1,
                CheckinDate = new DateTime(2025, 7, 15),
                CheckoutDate = new DateTime(2025, 7, 15),
                BookingSource = "walk-in",
                AmountReceived = 150,
                Notes = string.Empty,
                PaymentStatus = "Paid"
            });
        await context.SaveChangesAsync();

        var controller = new ReportsController(context, NullLogger<ReportsController>.Instance);
        var result = await controller.GetCalendarEarnings(1, "2025-07");
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<CalendarEarningEntry>>(ok.Value).ToList();
        Assert.Single(list);
        var entry = list.Single();
        Assert.Equal(new DateTime(2025, 7, 15), entry.Date);
        Assert.Equal(2, entry.Earnings.Count);
        Assert.All(entry.Earnings, e => Assert.Equal("walk-in", e.Source));
        Assert.Contains(entry.Earnings, e => e.Amount == 100);
        Assert.Contains(entry.Earnings, e => e.Amount == 150);
        Assert.Equal(2, entry.Earnings.Select(e => e.BookingId).Distinct().Count());
        Assert.All(entry.Earnings, e => Assert.Equal("Unknown Guest", e.GuestName));
        Assert.Equal(250, entry.Total);
    }

    [Fact]
    public async Task GetCalendarEarnings_ExcludesOutsideCalendarRange()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetCalendarEarnings_ExcludesOutsideCalendarRange))
            .Options;
        using var context = new AppDbContext(options);
        context.Bookings.AddRange(
            new Booking
            {
                ListingId = 1,
                GuestId = 1,
                CheckinDate = new DateTime(2025, 6, 28),
                CheckoutDate = new DateTime(2025, 6, 29),
                BookingSource = "airbnb",
                AmountReceived = 100,
                Notes = string.Empty,
                PaymentStatus = "Paid"
            },
            new Booking
            {
                ListingId = 1,
                GuestId = 1,
                CheckinDate = new DateTime(2025, 8, 10),
                CheckoutDate = new DateTime(2025, 8, 11),
                BookingSource = "airbnb",
                AmountReceived = 100,
                Notes = string.Empty,
                PaymentStatus = "Paid"
            },
            new Booking
            {
                ListingId = 1,
                GuestId = 1,
                CheckinDate = new DateTime(2025, 7, 1),
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
        var list = Assert.IsAssignableFrom<IEnumerable<CalendarEarningEntry>>(ok.Value).ToList();
        Assert.Single(list);
        var entry = list.Single();
        Assert.Equal(new DateTime(2025, 7, 1), entry.Date);
        Assert.Equal(200, entry.Total);
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
        Assert.Single(list);
        Assert.Equal(0, list[0].AmountReceived);
    }

    [Fact]
    public async Task GetBankAccountEarnings_ReturnsZero_WhenAccountHasNoBookings()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetBankAccountEarnings_ReturnsZero_WhenAccountHasNoBookings))
            .Options;
        using var context = new AppDbContext(options);
        context.BankAccounts.Add(new BankAccount { Id = 1, BankName = "Bank", AccountNumber = "123456", IFSC = "I", AccountType = "S" });
        await context.SaveChangesAsync();

        var controller = new ReportsController(context, NullLogger<ReportsController>.Instance);
        var result = await controller.GetBankAccountEarnings();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<BankAccountEarnings>>(ok.Value).ToList();
        Assert.Single(list);
        Assert.Equal(0, list[0].AmountReceived);
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
