
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;

namespace Atlas.Api.Controllers
{
    /// <summary>CRUD operations for properties.</summary>
    [ApiController]
    [Route("properties")]
    [Produces("application/json")]
    [Authorize]
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
        [ProducesResponseType(typeof(IEnumerable<PropertyResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<PropertyResponseDto>>> GetAll()
        {
            _logger.LogInformation("Fetching all properties");
            try
            {
                var properties = await _context.Properties.ToListAsync();
                _logger.LogInformation("Retrieved {Count} properties", properties.Count);
                return Ok(properties.Select(MapToResponseDto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving properties");
                return StatusCode(500, "Error retrieving properties");
            }
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(PropertyResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PropertyResponseDto>> Get(int id)
        {
            var item = await _context.Properties.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound();
            return MapToResponseDto(item);
        }

        [HttpPost]
        [ProducesResponseType(typeof(PropertyResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PropertyResponseDto>> Create(PropertyCreateDto dto)
        {
            var item = new Property
            {
                Name = dto.Name,
                Address = dto.Address,
                Type = dto.Type,
                OwnerName = dto.OwnerName,
                ContactPhone = dto.ContactPhone,
                CommissionPercent = dto.CommissionPercent,
                Status = dto.Status,
                TenantId = 0
            };
            _context.Properties.Add(item);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created property {PropertyId}: {Name}", item.Id, item.Name);
            return CreatedAtAction(nameof(Get), new { id = item.Id }, MapToResponseDto(item));
        }

        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, PropertyCreateDto dto)
        {
            var existing = await _context.Properties.FirstOrDefaultAsync(x => x.Id == id);
            if (existing == null) return NotFound();

            existing.Name = dto.Name;
            existing.Address = dto.Address;
            existing.Type = dto.Type;
            existing.OwnerName = dto.OwnerName;
            existing.ContactPhone = dto.ContactPhone;
            existing.CommissionPercent = dto.CommissionPercent;
            existing.Status = dto.Status;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.Properties.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound();

            var listingCount = await _context.Listings.CountAsync(l => l.PropertyId == id);
            if (listingCount > 0)
            {
                return Conflict(new ProblemDetails
                {
                    Status = 409,
                    Title = "Conflict",
                    Detail = $"Cannot delete property with {listingCount} active listing(s). Remove or reassign listings first.",
                    Instance = HttpContext.Request.Path
                });
            }

            _context.Properties.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static PropertyResponseDto MapToResponseDto(Property p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            Address = p.Address,
            Type = p.Type,
            OwnerName = p.OwnerName,
            ContactPhone = p.ContactPhone,
            CommissionPercent = p.CommissionPercent,
            Status = p.Status
        };
    }
}
