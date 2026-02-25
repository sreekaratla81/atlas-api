using Atlas.Api.Constants;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Api.Controllers
{
    /// <summary>Admin-level reports: bookings, payouts, earnings, sources.</summary>
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
        [ProducesResponseType(typeof(IEnumerable<BookingInfo>), StatusCodes.Status200OK)]
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
                BookingSource = b.BookingSource ?? string.Empty
            }).ToListAsync();

            return Ok(result);
        }

        [HttpGet("listings")]
        [ProducesResponseType(typeof(IEnumerable<ListingInfo>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ListingInfo>>> GetListings()
        {
            var result = await _context.Listings.Select(l => new ListingInfo
            {
                ListingId = l.Id,
                Name = l.Name,
                UnitCode = l.Type,
                IsActive = l.Status == ListingStatuses.Active
            }).ToListAsync();

            return Ok(result);
        }

        [HttpGet("payouts")]
        [ProducesResponseType(typeof(IEnumerable<DailyPayout>), StatusCodes.Status200OK)]
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
                    Listing = string.Empty,
                    Amount = g.Sum(x => x.p.Amount),
                    Status = CommunicationStatuses.Sent
                }).ToListAsync();

            return Ok(result);
        }

        [HttpGet("earnings/monthly")]
        [ProducesResponseType(typeof(IEnumerable<MonthlyEarningsSummary>), StatusCodes.Status200OK)]
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
                        TotalFees = g.Sum(x => x.AmountReceived * CommissionRates.ForSource(x.BookingSource))
                    })
                    .ToDictionary(g => g.Key, g => new { g.TotalNet, g.TotalFees });

                var result = monthKeys.Select(k => new MonthlyEarningsSummary
                {
                    Month = k,
                    TotalNet = grouped.TryGetValue(k, out var data) ? data.TotalNet : 0,
                    TotalFees = grouped.TryGetValue(k, out data) ? data.TotalFees : 0
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
        [ProducesResponseType(typeof(IEnumerable<MonthlyEarningsSummary>), StatusCodes.Status200OK)]
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
                        TotalFees = g.Sum(x => x.AmountReceived * CommissionRates.ForSource(x.BookingSource))
                    })
                    .ToDictionary(g => g.Key, g => new { g.TotalNet, g.TotalFees });

                var result = monthKeys.Select(k => new MonthlyEarningsSummary
                {
                    Month = k,
                    TotalNet = grouped.TryGetValue(k, out var data) ? data.TotalNet : 0,
                    TotalFees = grouped.TryGetValue(k, out data) ? data.TotalFees : 0
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
        [ProducesResponseType(typeof(IEnumerable<DailyPayout>), StatusCodes.Status200OK)]
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
                    Status = CommunicationStatuses.Sent
                }).ToListAsync();

            return Ok(result);
        }

        [HttpGet("bookings/source")]
        [ProducesResponseType(typeof(IEnumerable<SourceBookingSummary>), StatusCodes.Status200OK)]
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
                    Source = g.Key ?? "Unknown",
                    Count = g.Count()
                }).ToListAsync();

            return Ok(result);
        }

        [HttpGet("bookings/calendar")]
        [ProducesResponseType(typeof(IEnumerable<CalendarBooking>), StatusCodes.Status200OK)]
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
                Status = b.AmountReceived > 0 ? "Paid" : "Unpaid"
            }).ToListAsync();

            return Ok(result);
        }

        private static readonly string[] ActiveBookingStatuses =
        {
            BookingStatuses.Confirmed,
            BookingStatuses.CheckedIn,
            BookingStatuses.CheckedOut
        };

        [HttpGet("analytics")]
        [ProducesResponseType(typeof(AnalyticsDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<AnalyticsDto>> GetAnalytics(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] int? listingId)
        {
            var totalDays = (int)(endDate.Date - startDate.Date).TotalDays;
            if (totalDays <= 0)
                return BadRequest("endDate must be after startDate.");

            var listingsQuery = _context.Listings
                .Where(l => l.Status == ListingStatuses.Active);
            if (listingId.HasValue)
                listingsQuery = listingsQuery.Where(l => l.Id == listingId.Value);

            var listings = await listingsQuery
                .Select(l => new { l.Id, l.Name })
                .ToListAsync();

            var bookingsQuery = _context.Bookings
                .Where(b => ActiveBookingStatuses.Contains(b.BookingStatus))
                .Where(b => b.CheckinDate < endDate && b.CheckoutDate > startDate);
            if (listingId.HasValue)
                bookingsQuery = bookingsQuery.Where(b => b.ListingId == listingId.Value);

            var bookings = await bookingsQuery
                .Select(b => new { b.ListingId, b.CheckinDate, b.CheckoutDate, b.AmountReceived })
                .ToListAsync();

            var byListing = new List<ListingAnalytics>();
            foreach (var listing in listings)
            {
                var nightsAvailable = totalDays;
                var listingBookings = bookings.Where(b => b.ListingId == listing.Id).ToList();

                var nightsSold = listingBookings.Sum(b =>
                {
                    var effectiveStart = b.CheckinDate < startDate ? startDate.Date : b.CheckinDate.Date;
                    var effectiveEnd = b.CheckoutDate > endDate ? endDate.Date : b.CheckoutDate.Date;
                    return Math.Max(0, (int)(effectiveEnd - effectiveStart).TotalDays);
                });

                var revenue = listingBookings.Sum(b => b.AmountReceived);

                byListing.Add(new ListingAnalytics
                {
                    ListingId = listing.Id,
                    ListingName = listing.Name,
                    NightsAvailable = nightsAvailable,
                    NightsSold = nightsSold,
                    Revenue = revenue,
                    OccupancyRate = nightsAvailable > 0
                        ? Math.Round(nightsSold * 100m / nightsAvailable, 2) : 0,
                    Adr = nightsSold > 0
                        ? Math.Round(revenue / nightsSold, 2) : 0,
                    RevPar = nightsAvailable > 0
                        ? Math.Round(revenue / nightsAvailable, 2) : 0
                });
            }

            var totalNightsAvailable = byListing.Sum(l => l.NightsAvailable);
            var totalNightsSold = byListing.Sum(l => l.NightsSold);
            var totalRevenue = byListing.Sum(l => l.Revenue);

            return Ok(new AnalyticsDto
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalNightsAvailable = totalNightsAvailable,
                TotalNightsSold = totalNightsSold,
                TotalRevenue = totalRevenue,
                OccupancyRate = totalNightsAvailable > 0
                    ? Math.Round(totalNightsSold * 100m / totalNightsAvailable, 2) : 0,
                Adr = totalNightsSold > 0
                    ? Math.Round(totalRevenue / totalNightsSold, 2) : 0,
                RevPar = totalNightsAvailable > 0
                    ? Math.Round(totalRevenue / totalNightsAvailable, 2) : 0,
                ByListing = byListing
            });
        }

        [HttpGet("analytics/trends")]
        [ProducesResponseType(typeof(IEnumerable<MonthlyTrend>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<MonthlyTrend>>> GetAnalyticsTrends()
        {
            var now = DateTime.UtcNow;
            var startMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-11);

            var listings = await _context.Listings
                .Where(l => l.Status == ListingStatuses.Active)
                .CountAsync();

            var bookings = await _context.Bookings
                .Where(b => ActiveBookingStatuses.Contains(b.BookingStatus))
                .Where(b => b.CheckoutDate > startMonth && b.CheckinDate < startMonth.AddMonths(12))
                .Select(b => new { b.CheckinDate, b.CheckoutDate, b.AmountReceived })
                .ToListAsync();

            var trends = new List<MonthlyTrend>();
            for (int i = 0; i < 12; i++)
            {
                var monthStart = startMonth.AddMonths(i);
                var monthEnd = monthStart.AddMonths(1);
                var daysInMonth = (int)(monthEnd - monthStart).TotalDays;
                var nightsAvailable = daysInMonth * listings;

                var monthBookings = bookings
                    .Where(b => b.CheckinDate < monthEnd && b.CheckoutDate > monthStart)
                    .ToList();

                var nightsSold = monthBookings.Sum(b =>
                {
                    var effectiveStart = b.CheckinDate < monthStart ? monthStart : b.CheckinDate.Date;
                    var effectiveEnd = b.CheckoutDate > monthEnd ? monthEnd : b.CheckoutDate.Date;
                    return Math.Max(0, (int)(effectiveEnd - effectiveStart).TotalDays);
                });

                var revenue = monthBookings.Sum(b => b.AmountReceived);

                trends.Add(new MonthlyTrend
                {
                    Year = monthStart.Year,
                    Month = monthStart.Month,
                    OccupancyRate = nightsAvailable > 0
                        ? Math.Round(nightsSold * 100m / nightsAvailable, 2) : 0,
                    Adr = nightsSold > 0
                        ? Math.Round(revenue / nightsSold, 2) : 0,
                    RevPar = nightsAvailable > 0
                        ? Math.Round(revenue / nightsAvailable, 2) : 0,
                    Revenue = revenue,
                    BookingsCount = monthBookings.Count
                });
            }

            return Ok(trends);
        }

        [HttpGet("analytics/channel-performance")]
        [ProducesResponseType(typeof(IEnumerable<ChannelPerformance>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ChannelPerformance>>> GetChannelPerformance(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            var query = _context.Bookings
                .Where(b => ActiveBookingStatuses.Contains(b.BookingStatus));

            if (startDate.HasValue)
                query = query.Where(b => b.CheckinDate >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(b => b.CheckoutDate <= endDate.Value);

            var bookings = await query
                .Select(b => new { b.BookingSource, b.CheckinDate, b.CheckoutDate, b.AmountReceived })
                .ToListAsync();

            var totalRevenue = bookings.Sum(b => b.AmountReceived);

            var result = bookings
                .GroupBy(b => b.BookingSource ?? "Unknown")
                .Select(g =>
                {
                    var revenue = g.Sum(b => b.AmountReceived);
                    var nightsSold = g.Sum(b => Math.Max(0, (int)(b.CheckoutDate.Date - b.CheckinDate.Date).TotalDays));
                    return new ChannelPerformance
                    {
                        Channel = g.Key,
                        BookingsCount = g.Count(),
                        Revenue = revenue,
                        Adr = nightsSold > 0 ? Math.Round(revenue / nightsSold, 2) : 0,
                        SharePercent = totalRevenue > 0
                            ? Math.Round(revenue * 100m / totalRevenue, 2) : 0
                    };
                })
                .OrderByDescending(c => c.Revenue)
                .ToList();

            return Ok(result);
        }
    }
}
