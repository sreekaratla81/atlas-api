
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Constants;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;

namespace Atlas.Api.Controllers
{
    /// <summary>CRUD operations for payment records.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class PaymentsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PaymentsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<PaymentResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<PaymentResponseDto>>> GetAll(
            [FromQuery] int? bookingId,
            [FromQuery] DateTime? receivedFrom,
            [FromQuery] DateTime? receivedTo,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            const int maxPageSize = 500;
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 100;
            if (pageSize > maxPageSize) pageSize = maxPageSize;

            var query = _context.Payments.AsNoTracking().AsQueryable();
            if (bookingId.HasValue)
                query = query.Where(p => p.BookingId == bookingId.Value);
            if (receivedFrom.HasValue)
                query = query.Where(p => p.ReceivedOn >= receivedFrom.Value);
            if (receivedTo.HasValue)
                query = query.Where(p => p.ReceivedOn <= receivedTo.Value);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(p => p.ReceivedOn)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => MapToResponseDto(p))
                .ToListAsync();

            Response.Headers.Append("X-Total-Count", totalCount.ToString());
            Response.Headers.Append("X-Page", page.ToString());
            Response.Headers.Append("X-Page-Size", pageSize.ToString());
            return Ok(items);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PaymentResponseDto>> Get(int id)
        {
            var item = await _context.Payments.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound();
            return MapToResponseDto(item);
        }

        [HttpPost]
        [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PaymentResponseDto>> Create(PaymentCreateDto request)
        {
            if (request.BookingId <= 0)
                return BadRequest(new { error = "BookingId is required." });
            if (request.Amount <= 0)
                return BadRequest(new { error = "Amount must be greater than zero." });
            if (string.IsNullOrWhiteSpace(request.Method))
                return BadRequest(new { error = "Method is required." });

            var bookingExists = await _context.Bookings.AnyAsync(b => b.Id == request.BookingId);
            if (!bookingExists)
                return NotFound(new { error = $"Booking {request.BookingId} not found." });

            var item = new Payment
            {
                BookingId = request.BookingId,
                Amount = request.Amount,
                Method = request.Method,
                Type = request.Type,
                ReceivedOn = request.ReceivedOn ?? DateTime.UtcNow,
                Note = request.Note ?? string.Empty,
                RazorpayOrderId = request.RazorpayOrderId,
                RazorpayPaymentId = request.RazorpayPaymentId,
                RazorpaySignature = request.RazorpaySignature,
                Status = string.IsNullOrWhiteSpace(request.Status) ? PaymentStatuses.Pending : request.Status
            };
            _context.Payments.Add(item);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = item.Id }, MapToResponseDto(item));
        }

        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, PaymentUpdateDto request)
        {
            var item = await _context.Payments.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound();

            item.BookingId = request.BookingId;
            item.Amount = request.Amount;
            item.Method = request.Method;
            item.Type = request.Type;
            item.ReceivedOn = request.ReceivedOn;
            item.Note = request.Note;
            item.RazorpayOrderId = request.RazorpayOrderId;
            item.RazorpayPaymentId = request.RazorpayPaymentId;
            item.RazorpaySignature = request.RazorpaySignature;
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                item.Status = request.Status;
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.Payments.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound();
            _context.Payments.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static PaymentResponseDto MapToResponseDto(Payment p) => new()
        {
            Id = p.Id,
            BookingId = p.BookingId,
            Amount = p.Amount,
            BaseAmount = p.BaseAmount,
            DiscountAmount = p.DiscountAmount,
            ConvenienceFeeAmount = p.ConvenienceFeeAmount,
            Method = p.Method,
            Type = p.Type,
            ReceivedOn = p.ReceivedOn,
            Note = p.Note,
            RazorpayOrderId = p.RazorpayOrderId,
            RazorpayPaymentId = p.RazorpayPaymentId,
            Status = p.Status
        };
    }
}
