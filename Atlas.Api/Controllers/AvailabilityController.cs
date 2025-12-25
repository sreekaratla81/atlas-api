using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Atlas.Api.Data;
using Atlas.Api.DTOs;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("availability")]
    [Produces("application/json")]
    public class AvailabilityController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AvailabilityController> _logger;

        public AvailabilityController(AppDbContext context, ILogger<AvailabilityController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Gets booked date ranges for specified listings within a time window
        /// </summary>
        /// <param name="listingIds">Comma-separated list of listing IDs</param>
        /// <param name="daysAhead">Number of days ahead from today (default: 180)</param>
        /// <returns>Booked date ranges grouped by listing ID</returns>
        [HttpGet("booked-dates")]
        public async Task<ActionResult<BookedDatesResponse>> GetBookedDates(
            [FromQuery] string? listingIds,
            [FromQuery] int daysAhead = 180)
        {
            try
            {
                var fromDate = DateTime.UtcNow.Date;
                var toDate = fromDate.AddDays(daysAhead);

                var listingIdList = new List<int>();
                if (!string.IsNullOrWhiteSpace(listingIds))
                {
                    var parts = listingIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var part in parts)
                    {
                        if (int.TryParse(part, out var id))
                        {
                            listingIdList.Add(id);
                        }
                    }
                }

                var query = _context.Bookings
                    .AsNoTracking()
                    .Where(b => b.PaymentStatus != "Cancelled" 
                        && b.CheckinDate < toDate 
                        && b.CheckoutDate > fromDate);

                if (listingIdList.Any())
                {
                    query = query.Where(b => listingIdList.Contains(b.ListingId));
                }

                var bookings = await query
                    
                    .ToListAsync();

                var result = new BookedDatesResponse();
                foreach (var booking in bookings)
                {
                    if (!result.BookedDates.ContainsKey(booking.ListingId))
                    {
                        result.BookedDates[booking.ListingId] = new List<BookedDateRange>();
                    }

                    result.BookedDates[booking.ListingId].Add(new BookedDateRange
                    {
                        CheckinDate = booking.CheckinDate,
                        CheckoutDate = booking.CheckoutDate
                    });
                }

                // Sort date ranges by checkin date for each listing
                foreach (var listingId in result.BookedDates.Keys)
                {
                    result.BookedDates[listingId] = result.BookedDates[listingId]
                        .OrderBy(r => r.CheckinDate)
                        .ToList();
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving booked dates");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}


