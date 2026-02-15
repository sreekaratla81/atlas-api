
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.Models;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("properties")]
    [Produces("application/json")]
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
            var item = await _context.Properties.FirstOrDefaultAsync(x => x.Id == id);
            return item == null ? NotFound() : item;
        }

        [HttpPost]
        public async Task<ActionResult<Property>> Create(Property item)
        {
            if (item.TenantId != 0)
            {
                return BadRequest("TenantId is managed by the server.");
            }

            _context.Properties.Add(item);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, Property item)
        {
            if (id != item.Id) return BadRequest();

            var existing = await _context.Properties.FirstOrDefaultAsync(x => x.Id == id);
            if (existing == null) return NotFound();
            if (item.TenantId != 0 && item.TenantId != existing.TenantId)
            {
                return NotFound();
            }

            existing.Name = item.Name;
            existing.Address = item.Address;
            existing.Type = item.Type;
            existing.OwnerName = item.OwnerName;
            existing.ContactPhone = item.ContactPhone;
            existing.CommissionPercent = item.CommissionPercent;
            existing.Status = item.Status;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.Properties.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound();
            _context.Properties.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
