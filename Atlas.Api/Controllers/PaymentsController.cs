
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PaymentsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Payment>>> GetAll(
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

            var items = await query
                .OrderByDescending(p => p.ReceivedOn)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            return Ok(items);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Payment>> Get(int id)
        {
            var item = await _context.Payments.FirstOrDefaultAsync(x => x.Id == id);
            return item == null ? NotFound() : item;
        }

        [HttpPost]
        public async Task<ActionResult<Payment>> Create(PaymentCreateDto request)
        {
            var item = new Payment
            {
                BookingId = request.BookingId,
                Amount = request.Amount,
                Method = request.Method,
                Type = request.Type,
                ReceivedOn = request.ReceivedOn,
                Note = request.Note,
                RazorpayOrderId = request.RazorpayOrderId,
                RazorpayPaymentId = request.RazorpayPaymentId,
                RazorpaySignature = request.RazorpaySignature,
                Status = string.IsNullOrWhiteSpace(request.Status) ? "pending" : request.Status
            };
            _context.Payments.Add(item);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
        }

        [HttpPut("{id}")]
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
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.Payments.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound();
            _context.Payments.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
