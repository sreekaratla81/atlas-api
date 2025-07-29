using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Atlas.Api.DTOs;
using System.Linq;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("bookings")]
    [Produces("application/json")]
    public class BookingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BookingsController> _logger;

        public BookingsController(AppDbContext context, ILogger<BookingsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BookingDto>>> GetAll(
            [FromQuery] DateTime? checkinStart,
            [FromQuery] DateTime? checkinEnd)
        {
            try
            {
                var query = _context.Bookings
                    .AsNoTracking()
                    .AsQueryable();

                if (checkinStart.HasValue)
                    query = query.Where(b => b.CheckinDate >= checkinStart.Value);

                if (checkinEnd.HasValue)
                    query = query.Where(b => b.CheckinDate <= checkinEnd.Value);

                var bookings = await query
                    .Select(b => new BookingDto
                    {
                        Id = b.Id,
                        ListingId = b.ListingId,
                        GuestId = b.GuestId,
                        CheckinDate = b.CheckinDate,
                        CheckoutDate = b.CheckoutDate,
                        BookingSource = b.BookingSource,
                        AmountReceived = b.AmountReceived,
                        BankAccountId = b.BankAccountId,
                        GuestsPlanned = b.GuestsPlanned ?? 0,
                        GuestsActual = b.GuestsActual ?? 0,
                        ExtraGuestCharge = b.ExtraGuestCharge ?? 0,
                        AmountGuestPaid = b.AmountGuestPaid ?? 0,
                        CommissionAmount = b.CommissionAmount ?? 0,
                        Notes = b.Notes,
                        CreatedAt = b.CreatedAt,
                        PaymentStatus = b.PaymentStatus
                    })
                    .ToListAsync();

                return Ok(bookings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving bookings");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BookingDto>> Get(int id)
        {
            try
            {
                var item = await _context.Bookings
                    .Include(b => b.Listing)
                    .Include(b => b.Guest)
                    .FirstOrDefaultAsync(b => b.Id == id);
                return item == null ? NotFound() : Ok(MapToDto(item));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving booking {BookingId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<ActionResult<BookingDto>> Create([FromBody] CreateBookingRequest request)
        {
            try
            {
                var commissionRate = request.BookingSource.ToLower() switch
                {
                    "airbnb" => 0.16m,
                    "booking.com" => 0.15m,
                    "agoda" => 0.18m,
                    _ => 0m
                };

                var amountGuestPaid = request.AmountReceived + request.ExtraGuestCharge;
                var commissionAmount = amountGuestPaid * commissionRate;

                var booking = new Booking
                {
                    ListingId = request.ListingId,
                    GuestId = request.GuestId,
                    BookingSource = request.BookingSource,
                    PaymentStatus = string.IsNullOrWhiteSpace(request.PaymentStatus) ? "Pending" : request.PaymentStatus,
                    CheckinDate = request.CheckinDate,
                    CheckoutDate = request.CheckoutDate,
                    AmountReceived = request.AmountReceived,
                    BankAccountId = request.BankAccountId,
                    GuestsPlanned = request.GuestsPlanned,
                    GuestsActual = request.GuestsActual,
                    ExtraGuestCharge = request.ExtraGuestCharge,
                    AmountGuestPaid = amountGuestPaid,
                    CommissionAmount = commissionAmount,
                    Notes = request.Notes ?? string.Empty,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                var dto = MapToDto(booking);
                return CreatedAtAction(nameof(Get), new { id = booking.Id }, dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating booking");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Booking booking)
        {
            try
            {
                if (booking.Id == 0)
                {
                    booking.Id = id;
                }
                if (id != booking.Id) return BadRequest();

                var existingBooking = await _context.Bookings.FindAsync(id);
                if (existingBooking == null) return NotFound();

                // Update allowed fields
                existingBooking.GuestId = booking.GuestId;
                existingBooking.ListingId = booking.ListingId;
                existingBooking.CheckinDate = booking.CheckinDate;
                existingBooking.CheckoutDate = booking.CheckoutDate;
                existingBooking.BookingSource = booking.BookingSource;
                if (!string.IsNullOrWhiteSpace(booking.PaymentStatus))
                {
                    existingBooking.PaymentStatus = booking.PaymentStatus;
                }
                existingBooking.AmountReceived = booking.AmountReceived;
                existingBooking.GuestsPlanned = booking.GuestsPlanned;
                existingBooking.GuestsActual = booking.GuestsActual;
                existingBooking.ExtraGuestCharge = booking.ExtraGuestCharge;
                existingBooking.AmountGuestPaid = booking.AmountGuestPaid;
                existingBooking.CommissionAmount = booking.CommissionAmount;
                existingBooking.Notes = booking.Notes;
                existingBooking.BankAccountId = booking.BankAccountId;

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error updating booking {BookingId}", id);
                return StatusCode(500, "Concurrency error updating booking");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating booking {BookingId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var item = await _context.Bookings.FindAsync(id);
                if (item == null) return NotFound();
                _context.Bookings.Remove(item);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting booking {BookingId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        private static BookingDto MapToDto(Booking booking)
        {
            return new BookingDto
            {
                Id = booking.Id,
                ListingId = booking.ListingId,
                GuestId = booking.GuestId,
                CheckinDate = booking.CheckinDate,
                CheckoutDate = booking.CheckoutDate,
                BookingSource = booking.BookingSource,
                AmountReceived = booking.AmountReceived,
                BankAccountId = booking.BankAccountId,
                GuestsPlanned = booking.GuestsPlanned ?? 0,
                GuestsActual = booking.GuestsActual ?? 0,
                ExtraGuestCharge = booking.ExtraGuestCharge ?? 0,
                AmountGuestPaid = booking.AmountGuestPaid ?? 0,
                CommissionAmount = booking.CommissionAmount ?? 0,
                Notes = booking.Notes,
                CreatedAt = booking.CreatedAt,
                PaymentStatus = booking.PaymentStatus
            };
        }
    }
}
