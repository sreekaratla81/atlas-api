
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.Models;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("api/guests")]
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
            var item = await _context.Guests.FindAsync(id);
            return item == null ? NotFound() : item;
        }

        [HttpPost]
        public async Task<ActionResult<Guest>> Create(Guest item)
        {
            _context.Guests.Add(item);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, Guest item)
        {
            if (id != item.Id) return BadRequest();
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
