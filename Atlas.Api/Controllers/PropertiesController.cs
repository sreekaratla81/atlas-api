
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.Models;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PropertiesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PropertiesController> _logger;

        public PropertiesController(AppDbContext context, ILogger<PropertiesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Property>>> GetAll()
        {
            _logger.LogInformation("Fetching all properties");
            try
            {
                var properties = await _context.Properties.ToListAsync();
                _logger.LogInformation("Retrieved {Count} properties", properties.Count);
                return properties;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving properties");
                return StatusCode(500, "Error retrieving properties");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Property>> Get(int id)
        {
            var item = await _context.Properties.FindAsync(id);
            return item == null ? NotFound() : item;
        }

        [HttpPost]
        public async Task<ActionResult<Property>> Create(Property item)
        {
            _context.Properties.Add(item);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, Property item)
        {
            if (id != item.Id) return BadRequest();
            _context.Entry(item).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.Properties.FindAsync(id);
            if (item == null) return NotFound();
            _context.Properties.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
