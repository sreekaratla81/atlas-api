
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.Models;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IncidentsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public IncidentsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Incident>>> GetAll()
        {
            return await _context.Incidents.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Incident>> Get(int id)
        {
            var item = await _context.Incidents.FindAsync(id);
            return item == null ? NotFound() : item;
        }

        [HttpPost]
        public async Task<ActionResult<Incident>> Create(Incident item)
        {
            _context.Incidents.Add(item);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, Incident item)
        {
            if (id != item.Id) return BadRequest();
            _context.Entry(item).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.Incidents.FindAsync(id);
            if (item == null) return NotFound();
            _context.Incidents.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
