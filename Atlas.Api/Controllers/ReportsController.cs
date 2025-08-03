using Atlas.Api.Data;
using Atlas.Api.Models.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("reports")]
    [Produces("application/json")]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(AppDbContext context, ILogger<ReportsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("calendar-earnings")]
        public async Task<ActionResult<IEnumerable<CalendarEarningEntry>>> GetCalendarEarnings([
            FromQuery] int listingId,
            [FromQuery] string month)
        {
            if (!DateTime.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var monthStart))
            {
                return BadRequest("Invalid month format. Use yyyy-MM.");
            }

            var startOffset = ((int)monthStart.DayOfWeek + 7 - (int)DayOfWeek.Sunday) % 7;
            var calendarStart = monthStart.AddDays(-startOffset);
            var calendarEnd = calendarStart.AddDays(42);

            var bookings = await _context.Bookings
                .Include(b => b.Guest)
                .Where(b => b.ListingId == listingId &&
                            b.CheckinDate < calendarEnd &&
                            b.CheckoutDate > calendarStart)
                .ToListAsync();

            var result = new Dictionary<DateTime, List<BookingEarningDetail>>();
            foreach (var booking in bookings)
            {
                var start = booking.CheckinDate.Date;
                var end = booking.CheckoutDate.Date;
                var nights = (end - start).TotalDays;

                var guestName = booking.Guest?.Name;
                if (string.IsNullOrWhiteSpace(guestName))
                {
                    guestName = "Unknown";
                }

                if (nights <= 0)
                {
                    var day = start;
                    if (day >= calendarStart && day < calendarEnd)
                    {
                        if (!result.TryGetValue(day, out var list))
                        {
                            list = new List<BookingEarningDetail>();
                            result[day] = list;
                        }
                        list.Add(new BookingEarningDetail
                        {
                            Source = booking.BookingSource,
                            Amount = Math.Round(booking.AmountReceived, 2),
                            GuestName = guestName,
                            CheckinDate = booking.CheckinDate
                        });
                    }
                    continue;
                }

                var dailyAmount = Math.Round(booking.AmountReceived / (decimal)nights, 2);
                for (var day = start; day < end; day = day.AddDays(1))
                {
                    if (day >= calendarStart && day < calendarEnd)
                    {
                        if (!result.TryGetValue(day, out var list))
                        {
                            list = new List<BookingEarningDetail>();
                            result[day] = list;
                        }
                        list.Add(new BookingEarningDetail
                        {
                            Source = booking.BookingSource,
                            Amount = dailyAmount,
                            GuestName = guestName,
                            CheckinDate = booking.CheckinDate
                        });
                    }
                }
            }

            var entries = result
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => new CalendarEarningEntry { Date = kvp.Key, Earnings = kvp.Value })
                .ToList();

            return Ok(entries);
        }

        [HttpGet("bank-account-earnings")]
        public async Task<ActionResult<IEnumerable<Atlas.Api.Models.Reports.BankAccountEarnings>>> GetBankAccountEarnings()
        {
            var fyStart = new DateTime(2025, 4, 1);
            var fyEnd = new DateTime(2026, 4, 1);

            var result = await _context.BankAccounts
                .Select(account => new Atlas.Api.Models.Reports.BankAccountEarnings
                {
                    Bank = account.BankName,
                    AccountDisplay = account.BankName + " - " +
                        (account.AccountNumber.Length >= 4
                            ? account.AccountNumber.Substring(account.AccountNumber.Length - 4)
                            : account.AccountNumber),
                    AmountReceived = _context.Bookings
                        .Where(b =>
                            b.BankAccountId == account.Id &&
                            b.CheckinDate >= fyStart &&
                            b.CheckinDate < fyEnd)
                        .Sum(b => (decimal?)b.AmountReceived) ?? 0
                })
                .OrderBy(r => r.Bank)
                .ToListAsync();

            return Ok(result);
        }
    }
}
