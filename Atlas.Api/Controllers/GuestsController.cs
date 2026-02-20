
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.Models;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("guests")]
    [Produces("application/json")]
    public class GuestsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public GuestsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Guest>>> GetAll()
        {
            return await _context.Guests.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Guest>> Get(int id)
        {
            var item = await _context.Guests.FirstOrDefaultAsync(x => x.Id == id);
            return item == null ? NotFound() : item;
        }

        [HttpPost]
        public async Task<ActionResult<Guest>> Create(Guest item)
        {
            item.TenantId = 0;
            if (string.IsNullOrWhiteSpace(item.IdProofUrl))
            {
                item.IdProofUrl = "N/A";
            }

            _context.Guests.Add(item);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, Guest item)
        {
            if (id != item.Id) return BadRequest();
            var existing = await _context.Guests.FirstOrDefaultAsync(x => x.Id == id);
            if (existing == null) return NotFound();
            item.TenantId = existing.TenantId;

            existing.Name = item.Name;
            existing.Phone = item.Phone;
            existing.Email = item.Email;
            existing.IdProofUrl = item.IdProofUrl;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.Guests.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound();
            _context.Guests.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
