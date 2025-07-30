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
        public async Task<ActionResult<IEnumerable<DailySourceEarnings>>> GetCalendarEarnings([
            FromQuery] int listingId,
            [FromQuery] string month)
        {
            if (!DateTime.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var monthStart))
            {
                return BadRequest("Invalid month format. Use yyyy-MM.");
            }
            var monthEnd = monthStart.AddMonths(1);

            var bookings = await _context.Bookings
                .Where(b => b.ListingId == listingId &&
                            b.CheckinDate < monthEnd &&
                            b.CheckoutDate > monthStart)
                .Select(b => new { b.CheckinDate, b.CheckoutDate, b.AmountReceived, b.BookingSource })
                .ToListAsync();

            var result = new Dictionary<(DateTime Date, string Source), decimal>();
            foreach (var b in bookings)
            {
                var nights = (b.CheckoutDate.Date - b.CheckinDate.Date).TotalDays;
                if (nights <= 0) continue;
                var dailyAmount = b.AmountReceived / (decimal)nights;
                for (var day = b.CheckinDate.Date; day < b.CheckoutDate.Date; day = day.AddDays(1))
                {
                    if (day >= monthStart && day < monthEnd)
                    {
                        var key = (day, b.BookingSource);
                        result.TryGetValue(key, out var current);
                        result[key] = current + dailyAmount;
                    }
                }
            }

            var rounded = result.Select(kvp => new DailySourceEarnings
            {
                Date = kvp.Key.Date.ToString("yyyy-MM-dd"),
                Source = kvp.Key.Source,
                Amount = Math.Round(kvp.Value, 2)
            }).ToList();
            return Ok(rounded);
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
