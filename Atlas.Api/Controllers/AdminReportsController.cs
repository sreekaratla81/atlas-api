using Atlas.Api.Data;
using Atlas.Api.Models.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("")]
    [Produces("application/json")]
    [Authorize]
    public class AdminReportsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminReportsController(AppDbContext context)
        {
            _context = context;
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

        [HttpPost("reports/earnings/monthly")]
        public async Task<ActionResult<IEnumerable<MonthlyEarningsSummary>>> GetMonthlyEarnings([FromBody] ReportFilter filter)
        {
            var query = from p in _context.Payments
                        join b in _context.Bookings on p.BookingId equals b.Id
                        select new { p, b };
            if (filter.StartDate.HasValue)
                query = query.Where(x => x.p.ReceivedOn >= filter.StartDate.Value);
            if (filter.EndDate.HasValue)
                query = query.Where(x => x.p.ReceivedOn <= filter.EndDate.Value);
            if (filter.ListingIds != null && filter.ListingIds.Any())
                query = query.Where(x => filter.ListingIds.Contains(x.b.ListingId));

            var result = await query.GroupBy(x => x.p.ReceivedOn.ToString("yyyy-MM"))
                .Select(g => new MonthlyEarningsSummary
                {
                    Month = g.Key,
                    TotalGross = g.Sum(x => x.p.Amount),
                    TotalFees = 0,
                    TotalNet = g.Sum(x => x.p.Amount)
                }).ToListAsync();

            return Ok(result);
        }

        [HttpGet("reports/payouts/daily")]
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

        [HttpGet("reports/bookings/source")]
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

        [HttpGet("reports/bookings/calendar")]
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
