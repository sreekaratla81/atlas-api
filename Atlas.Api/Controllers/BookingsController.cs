using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.Models;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("api/bookings")]
    public class BookingsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BookingsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Booking>>> GetAll()
        {
            return await _context.Bookings.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Booking>> Get(int id)
        {
            var item = await _context.Bookings.FindAsync(id);
            return item == null ? NotFound() : item;
        }

        [HttpPost]
        public async Task<ActionResult<Booking>> Create([FromBody] Booking booking)
        {
            try
            {
                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();
                return CreatedAtAction(nameof(Get), new { id = booking.Id }, booking);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Booking booking)
        {
            if (id != booking.Id) return BadRequest();

            var existingBooking = await _context.Bookings.FindAsync(id);
            if (existingBooking == null) return NotFound();

            // Update allowed fields
            existingBooking.CheckinDate = booking.CheckinDate;
            existingBooking.CheckoutDate = booking.CheckoutDate;
            existingBooking.PlannedCheckinTime = booking.PlannedCheckinTime;
            existingBooking.ActualCheckinTime = booking.ActualCheckinTime;
            existingBooking.PlannedCheckoutTime = booking.PlannedCheckoutTime;
            existingBooking.ActualCheckoutTime = booking.ActualCheckoutTime;
            existingBooking.BookingSource = booking.BookingSource;
            existingBooking.PaymentStatus = booking.PaymentStatus;
            existingBooking.AmountReceived = booking.AmountReceived;
            existingBooking.Notes = booking.Notes;

            try
            {
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(500, "A concurrency error occurred while updating the booking.");
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
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
