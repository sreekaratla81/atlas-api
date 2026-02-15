
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Atlas.Api.Data;
using Atlas.Api.Models;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("listings")]
    [Produces("application/json")]
    public class ListingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ListingsController> _logger;

        public ListingsController(AppDbContext context, ILogger<ListingsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Listing>>> GetAll()
        {
            try
            {
                var listings = await _context.Listings
                    .Include(l => l.Property)
                    .ToListAsync();
                return Ok(listings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving listings");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("public")]
        public async Task<ActionResult<IEnumerable<Listing>>> GetPublicListings()
        {
            try
            {
                var listings = await _context.Listings
                    .Include(l => l.Property)
                    .Where(l => l.Status == "Active")
                    .ToListAsync();
                return Ok(listings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving public listings");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Listing>> Get(int id)
        {
            try
            {
                var item = await _context.Listings
                    .Include(l => l.Property)
                    .FirstOrDefaultAsync(l => l.Id == id);
                return item == null ? NotFound() : Ok(item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving listing {ListingId}", id);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost]
        public async Task<ActionResult<Listing>> Create(Listing item)
        {
            if (item.TenantId != 0)
            {
                return BadRequest("TenantId is managed by the server.");
            }
            try
            {
                // Ensure the associated Property exists and attach it to the context
                var property = await _context.Properties.FirstOrDefaultAsync(x => x.Id == item.PropertyId);
                if (property == null)
                {
                    return BadRequest();
                }

                // Replace any deserialized Property instance with the tracked entity
                item.Property = property;

                _context.Listings.Add(item);
                await _context.SaveChangesAsync();
                return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating listing");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, Listing item)
        {
            try
            {
                if (id != item.Id) return BadRequest();

                var existing = await _context.Listings.FirstOrDefaultAsync(x => x.Id == id);
                if (existing == null) return NotFound();
                if (item.TenantId != 0 && item.TenantId != existing.TenantId)
                {
                    return NotFound();
                }

                var property = await _context.Properties.FirstOrDefaultAsync(x => x.Id == item.PropertyId);
                if (property == null)
                {
                    return NotFound();
                }

                existing.PropertyId = item.PropertyId;
                existing.Property = property;
                existing.Name = item.Name;
                existing.Floor = item.Floor;
                existing.Type = item.Type;
                existing.CheckInTime = item.CheckInTime;
                existing.CheckOutTime = item.CheckOutTime;
                existing.Status = item.Status;
                existing.WifiName = item.WifiName;
                existing.WifiPassword = item.WifiPassword;
                existing.MaxGuests = item.MaxGuests;

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating listing {ListingId}", id);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var item = await _context.Listings.FirstOrDefaultAsync(x => x.Id == id);
                if (item == null) return NotFound();
                _context.Listings.Remove(item);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting listing {ListingId}", id);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
