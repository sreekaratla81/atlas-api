using Atlas.Api.Data;
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
        public async Task<ActionResult<Dictionary<string, decimal>>> GetCalendarEarnings([
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
                .Select(b => new { b.CheckinDate, b.CheckoutDate, b.AmountReceived })
                .ToListAsync();

            var result = new Dictionary<string, decimal>();
            foreach (var b in bookings)
            {
                var nights = (b.CheckoutDate.Date - b.CheckinDate.Date).TotalDays;
                if (nights <= 0) continue;
                var dailyAmount = b.AmountReceived / (decimal)nights;
                for (var day = b.CheckinDate.Date; day < b.CheckoutDate.Date; day = day.AddDays(1))
                {
                    if (day >= monthStart && day < monthEnd)
                    {
                        var key = day.ToString("yyyy-MM-dd");
                        result.TryGetValue(key, out var current);
                        result[key] = current + dailyAmount;
                    }
                }
            }

            var rounded = result.ToDictionary(kvp => kvp.Key, kvp => Math.Round(kvp.Value, 2));
            return Ok(rounded);
        }
    }
}
