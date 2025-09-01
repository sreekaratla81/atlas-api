using Application.Guests.Queries.SearchGuests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Infrastructure.Phone;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("guests")]
    [Produces("application/json")]
    public class GuestsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly SearchGuestsQueryHandler _handler;
        private readonly PhoneNormalizer _phoneNormalizer;

        public GuestsController(AppDbContext context, SearchGuestsQueryHandler handler, PhoneNormalizer phoneNormalizer)
        {
            _context = context;
            _handler = handler;
            _phoneNormalizer = phoneNormalizer;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Guest>>> GetAll()
        {
            return await _context.Guests.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Guest>> Get(int id)
        {
            var item = await _context.Guests.FindAsync(id);
            return item == null ? NotFound() : item;
        }

        [Authorize]
        [EnableRateLimiting("SearchGuestsLimit")]
        [HttpGet("search")]
        public async Task<ActionResult<SearchGuestsResponse>> Search([FromQuery] string query, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? fields = null, CancellationToken cancellationToken = default)
        {
            var trimmed = (query ?? string.Empty).Trim();
            var digitsOnly = trimmed.All(char.IsDigit);
            if (!digitsOnly && !trimmed.Contains('@') && trimmed.Length < 2)
            {
                return BadRequest("Query must be at least 2 characters.");
            }
            pageSize = Math.Clamp(pageSize, 5, 25);
            var request = new SearchGuestsRequest(trimmed, page, pageSize, fields);
            var result = await _handler.Handle(request, cancellationToken);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<Guest>> Create(Guest item)
        {
            if (string.IsNullOrWhiteSpace(item.IdProofUrl))
            {
                item.IdProofUrl = "N/A";
            }
            item.NameSearch = item.Name.ToLowerInvariant();
            item.PhoneE164 = _phoneNormalizer.Normalize(item.Phone);

            _context.Guests.Add(item);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, Guest item)
        {
            if (id != item.Id) return BadRequest();
            item.NameSearch = item.Name.ToLowerInvariant();
            item.PhoneE164 = _phoneNormalizer.Normalize(item.Phone);
            _context.Entry(item).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.Guests.FindAsync(id);
            if (item == null) return NotFound();
            _context.Guests.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
