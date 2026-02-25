
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Atlas.Api.Constants;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;

namespace Atlas.Api.Controllers
{
    /// <summary>CRUD operations for listings within properties.</summary>
    [ApiController]
    [Route("listings")]
    [Produces("application/json")]
    [Authorize]
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
        [ProducesResponseType(typeof(IEnumerable<ListingResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ListingResponseDto>>> GetAll()
        {
            try
            {
                var listings = await _context.Listings
                    .Include(l => l.Property)
                    .ToListAsync();
                return Ok(listings.Select(MapToDto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving listings");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("public")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IEnumerable<PublicListingDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<PublicListingDto>>> GetPublicListings()
        {
            try
            {
                var listings = await _context.Listings
                    .AsNoTracking()
                    .Where(l => l.Status == ListingStatuses.Active)
                    .Select(l => new PublicListingDto
                    {
                        Id = l.Id,
                        PropertyId = l.PropertyId,
                        PropertyName = l.Property != null ? l.Property.Name : "",
                        PropertyAddress = l.Property != null ? l.Property.Address : null,
                        Name = l.Name,
                        Floor = l.Floor,
                        Type = l.Type,
                        CheckInTime = l.CheckInTime,
                        CheckOutTime = l.CheckOutTime,
                        Status = l.Status,
                        MaxGuests = l.MaxGuests
                    })
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
        [ProducesResponseType(typeof(ListingResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ListingResponseDto>> Get(int id)
        {
            try
            {
                var item = await _context.Listings
                    .Include(l => l.Property)
                    .FirstOrDefaultAsync(l => l.Id == id);
                return item == null ? NotFound() : Ok(MapToDto(item));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving listing {ListingId}", id);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost]
        [ProducesResponseType(typeof(Listing), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Listing>> Create(Listing item)
        {
            item.TenantId = 0;
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
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, Listing item)
        {
            try
            {
                if (id != item.Id) return BadRequest();

                var existing = await _context.Listings.FirstOrDefaultAsync(x => x.Id == id);
                if (existing == null) return NotFound();
                item.TenantId = existing.TenantId;

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
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.Listings.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound();

            var bookingCount = await _context.Bookings.CountAsync(b => b.ListingId == id);
            var blockCount = await _context.AvailabilityBlocks.CountAsync(ab => ab.ListingId == id);

            if (bookingCount > 0 || blockCount > 0)
            {
                var parts = new List<string>();
                if (bookingCount > 0) parts.Add($"{bookingCount} booking(s)");
                if (blockCount > 0) parts.Add($"{blockCount} availability block(s)");

                return Conflict(new ProblemDetails
                {
                    Status = 409,
                    Title = "Conflict",
                    Detail = $"Cannot delete listing with {string.Join(" and ", parts)}. Remove related records first.",
                    Instance = HttpContext.Request.Path
                });
            }

            _context.Listings.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static ListingResponseDto MapToDto(Listing listing) => new()
        {
            Id = listing.Id,
            PropertyId = listing.PropertyId,
            Name = listing.Name,
            Floor = listing.Floor,
            Type = listing.Type,
            Status = listing.Status,
            MaxGuests = listing.MaxGuests
        };
    }
}
