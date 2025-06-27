using Atlas.Api.Data;
using Atlas.Api.Models.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("admin/reports")]
    [Produces("application/json")]
    [AllowAnonymous]
    public class AdminReportsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AdminReportsController> _logger;

        public AdminReportsController(AppDbContext context, ILogger<AdminReportsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("bookings")]
        public async Task<ActionResult<IEnumerable<BookingInfo>>> GetBookings(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] List<int>? listingIds)
        {
            var query = _context.Bookings.AsQueryable();
            if (startDate.HasValue)
                query = query.Where(b => b.CheckinDate >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(b => b.CheckoutDate <= endDate.Value);
            if (listingIds != null && listingIds.Any())
                query = query.Where(b => listingIds.Contains(b.ListingId));

            var result = await query.Select(b => new BookingInfo
            {
                BookingId = b.Id,
                CheckInDate = b.CheckinDate,
                CheckOutDate = b.CheckoutDate,
                AmountReceived = b.AmountReceived,
                ListingId = b.ListingId,
                BookingSource = b.BookingSource
            }).ToListAsync();

            return Ok(result);
        }

        [HttpGet("listings")]
        public async Task<ActionResult<IEnumerable<ListingInfo>>> GetListings()
        {
            var result = await _context.Listings.Select(l => new ListingInfo
            {
                ListingId = l.Id,
                Name = l.Name,
                UnitCode = l.Type,
                IsActive = l.Status == "Active"
            }).ToListAsync();

            return Ok(result);
        }

        [HttpGet("payouts")]
        public async Task<ActionResult<IEnumerable<DailyPayout>>> GetPayouts(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] List<int>? listingIds)
        {
            var query = from p in _context.Payments
                        join b in _context.Bookings on p.BookingId equals b.Id
                        select new { p, b };
            if (startDate.HasValue)
                query = query.Where(x => x.p.ReceivedOn.Date >= startDate.Value.Date);
            if (endDate.HasValue)
                query = query.Where(x => x.p.ReceivedOn.Date <= endDate.Value.Date);
            if (listingIds != null && listingIds.Any())
                query = query.Where(x => listingIds.Contains(x.b.ListingId));

            var result = await query.GroupBy(x => new { Date = x.p.ReceivedOn.Date, x.b.ListingId })
                .Select(g => new DailyPayout
                {
                    Date = g.Key.Date,
                    ListingId = g.Key.ListingId,
                    Amount = g.Sum(x => x.p.Amount),
                    Status = "Sent"
                }).ToListAsync();

            return Ok(result);
        }

        [HttpGet("earnings/monthly")]
        public async Task<ActionResult<IEnumerable<MonthlyEarningsSummary>>> GetMonthlyEarnings()
        {
            try
            {
                var endDate = DateTime.UtcNow;
                var startDate = new DateTime(endDate.Year, endDate.Month, 1).AddMonths(-11);

                var monthKeys = Enumerable.Range(0, 12)
                    .Select(i => startDate.AddMonths(i))
                    .Select(d => d.ToString("yyyy-MM"))
                    .ToList();

                var bookings = await _context.Bookings
                    .Where(b => b.CheckinDate >= startDate && b.CheckinDate < startDate.AddMonths(12))
                    .Select(b => new { b.CheckinDate, b.AmountReceived, b.BookingSource })
                    .ToListAsync();

                var grouped = bookings
                    .GroupBy(b => new { b.CheckinDate.Year, b.CheckinDate.Month })
                    .Select(g => new
                    {
                        Key = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("yyyy-MM"),
                        TotalNet = g.Sum(x => x.AmountReceived),
                        TotalFees = g.Sum(x =>
                            x.BookingSource.ToLower() switch
                            {
                                "airbnb" => x.AmountReceived * 0.16m,
                                "booking.com" => x.AmountReceived * 0.15m,
                                "agoda" => x.AmountReceived * 0.18m,
                                _ => 0m
                            })
                    })
                    .ToDictionary(g => g.Key, g => new { g.TotalNet, g.TotalFees });

                var result = monthKeys.Select(k => new MonthlyEarningsSummary
                {
                    Month = k,
                    TotalNet = grouped.TryGetValue(k, out var data) ? data.TotalNet : 0,
                    TotalFees = grouped.TryGetValue(k, out data) ? data.TotalFees : 0,
                    TotalGross = grouped.TryGetValue(k, out data) ? data.TotalNet + data.TotalFees : 0
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate monthly earnings report");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("earnings/monthly")]
        public async Task<ActionResult<IEnumerable<MonthlyEarningsSummary>>> GetMonthlyEarnings([FromBody] ReportFilter filter)
        {
            try
            {
                var endDate = filter.EndDate ?? DateTime.UtcNow;
                var startDate = filter.StartDate ?? new DateTime(endDate.Year, endDate.Month, 1).AddMonths(-11);

                var monthKeys = Enumerable.Range(0, 12)
                    .Select(i => startDate.AddMonths(i))
                    .Select(d => d.ToString("yyyy-MM"))
                    .ToList();

                var query = _context.Bookings.AsQueryable();
                query = query.Where(b => b.CheckinDate >= startDate && b.CheckinDate < startDate.AddMonths(12));
                if (filter.ListingIds != null && filter.ListingIds.Any())
                    query = query.Where(b => filter.ListingIds.Contains(b.ListingId));

                var bookings = await query
                    .Select(b => new { b.CheckinDate, b.AmountReceived, b.BookingSource })
                    .ToListAsync();

                var grouped = bookings
                    .GroupBy(b => new { b.CheckinDate.Year, b.CheckinDate.Month })
                    .Select(g => new
                    {
                        Key = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("yyyy-MM"),
                        TotalNet = g.Sum(x => x.AmountReceived),
                        TotalFees = g.Sum(x =>
                            x.BookingSource.ToLower() switch
                            {
                                "airbnb" => x.AmountReceived * 0.16m,
                                "booking.com" => x.AmountReceived * 0.15m,
                                "agoda" => x.AmountReceived * 0.18m,
                                _ => 0m
                            })
                    })
                    .ToDictionary(g => g.Key, g => new { g.TotalNet, g.TotalFees });

                var result = monthKeys.Select(k => new MonthlyEarningsSummary
                {
                    Month = k,
                    TotalNet = grouped.TryGetValue(k, out var data) ? data.TotalNet : 0,
                    TotalFees = grouped.TryGetValue(k, out data) ? data.TotalFees : 0,
                    TotalGross = grouped.TryGetValue(k, out data) ? data.TotalNet + data.TotalFees : 0
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate monthly earnings report");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("payouts/daily")]
        public async Task<ActionResult<IEnumerable<DailyPayout>>> GetDailyPayoutReport(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] List<int>? listingIds)
        {
            var query = from p in _context.Payments
                        join b in _context.Bookings on p.BookingId equals b.Id
                        join l in _context.Listings on b.ListingId equals l.Id
                        select new { p, b, l };
            if (startDate.HasValue)
                query = query.Where(x => x.p.ReceivedOn.Date >= startDate.Value.Date);
            if (endDate.HasValue)
                query = query.Where(x => x.p.ReceivedOn.Date <= endDate.Value.Date);
            if (listingIds != null && listingIds.Any())
                query = query.Where(x => listingIds.Contains(x.b.ListingId));

            var result = await query.GroupBy(x => new { x.p.ReceivedOn.Date, x.l.Name, x.b.ListingId })
                .Select(g => new DailyPayout
                {
                    Date = g.Key.Date,
                    Listing = g.Key.Name,
                    ListingId = g.Key.ListingId,
                    Amount = g.Sum(x => x.p.Amount),
                    Status = "Sent"
                }).ToListAsync();

            return Ok(result);
        }

        [HttpGet("bookings/source")]
        public async Task<ActionResult<IEnumerable<SourceBookingSummary>>> GetBookingSourceReport(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] List<int>? listingIds)
        {
            var query = _context.Bookings.AsQueryable();
            if (startDate.HasValue)
                query = query.Where(b => b.CheckinDate >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(b => b.CheckoutDate <= endDate.Value);
            if (listingIds != null && listingIds.Any())
                query = query.Where(b => listingIds.Contains(b.ListingId));

            var result = await query.GroupBy(b => b.BookingSource)
                .Select(g => new SourceBookingSummary
                {
                    Source = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(b => b.AmountReceived)
                }).ToListAsync();

            return Ok(result);
        }

        [HttpGet("bookings/calendar")]
        public async Task<ActionResult<IEnumerable<CalendarBooking>>> GetCalendarBookings(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] List<int>? listingIds)
        {
            var query = _context.Bookings.AsQueryable();
            if (startDate.HasValue)
                query = query.Where(b => b.CheckinDate >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(b => b.CheckoutDate <= endDate.Value);
            if (listingIds != null && listingIds.Any())
                query = query.Where(b => listingIds.Contains(b.ListingId));

            var result = await query.Select(b => new CalendarBooking
            {
                ListingId = b.ListingId,
                Date = b.CheckinDate,
                Amount = b.AmountReceived,
                Status = b.PaymentStatus
            }).ToListAsync();

            return Ok(result);
        }
    }
}
