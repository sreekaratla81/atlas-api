using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Atlas.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("availability")]
    [Produces("application/json")]
    public class AvailabilityController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AvailabilityService _availabilityService;
        private readonly ILogger<AvailabilityController> _logger;

        public AvailabilityController(AppDbContext context, AvailabilityService availabilityService, ILogger<AvailabilityController> logger)
        {
            _context = context;
            _availabilityService = availabilityService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<AvailabilityResponseDto>> GetAvailability(
            [FromQuery] int propertyId,
            [FromQuery] DateTime checkIn,
            [FromQuery] DateTime checkOut,
            [FromQuery] int guests)
        {
            if (propertyId <= 0)
            {
                ModelState.AddModelError(nameof(propertyId), "PropertyId is required.");
                return BadRequest(ModelState);
            }

            if (guests <= 0)
            {
                ModelState.AddModelError(nameof(guests), "Guests must be at least 1.");
                return BadRequest(ModelState);
            }

            if (checkOut.Date <= checkIn.Date)
            {
                ModelState.AddModelError(nameof(checkOut), "Checkout must be after check-in.");
                return BadRequest(ModelState);
            }

            try
            {
                var propertyExists = await _context.Properties
                    .AsNoTracking()
                    .AnyAsync(p => p.Id == propertyId);

                if (!propertyExists)
                {
                    return NotFound();
                }

                var response = await _availabilityService.GetAvailabilityAsync(propertyId, checkIn, checkOut, guests);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving availability for property {PropertyId}", propertyId);
                return StatusCode(500, "Internal server error");
            }
        }
        [HttpGet("listing-availability")]
        public async Task<IActionResult> GetListingAvailability(
            [FromQuery] int listingId,
    [FromQuery] DateTime startDate)
        {
            if (listingId <= 0)
            {
                return BadRequest(new { message = "Listing ID must be greater than 0" });
            }

            try
            {
                
                // Get the listing details
                var listing = await _context.Listings
                    .AsNoTracking()
                    .Where(l => l.Id == listingId)
                    .Select(l => new { l.Id, l.Name })
                    .FirstOrDefaultAsync();

                if (listing == null)
                {
                    return NotFound(new { message = "Listing not found" });
                }

                // Get the availability for the date
                var availability = await _context.AvailabilityBlocks
                    .AsNoTracking()
                    .Where(ab => ab.ListingId == listingId &&
                                ab.StartDate.Date <= startDate.Date &&
                                ab.EndDate.Date >= startDate.Date)
                    .Select(ab => new
                    {
                        ab.Status,
                        ab.Inventory
                    })
                    .FirstOrDefaultAsync();

               

                var response = new
                {
                    listingId = listing.Id,
                    listingName = listing.Name,
                    date = startDate.Date.ToString("yyyy-MM-dd"),
                    status = availability?.Status ?? "Available",
                    inventory = availability != null ? (availability.Inventory ? 1 : 0) : 1

                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving listing availability for listing {ListingId} on {Date}",
                    listingId, startDate);
                return StatusCode(500, new
                {
                    message = "An error occurred while retrieving availability information",
                    error = ex.Message
                });
            }
        }

        [HttpPost("blocks")]
        public async Task<IActionResult> BlockAvailability(
    [FromBody] AvailabilityBlockRequestDto request)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var dates = new List<DateTime>();
                for (var d = request.StartDate.Date; d < request.EndDate.Date; d = d.AddDays(1))
                    dates.Add(d);

                var now = DateTime.UtcNow;

                // 🔹 Fetch ALL existing rows for requested dates in ONE query
                var existingRows = await _context.AvailabilityBlocks
                    .Where(ab =>
                        ab.ListingId == request.ListingId &&
                        ab.StartDate >= request.StartDate.Date &&
                        ab.StartDate < request.EndDate.Date)
                    .ToListAsync();

                foreach (var date in dates)
                {
                    var existing = existingRows.FirstOrDefault(ab =>
                        ab.StartDate >= date &&
                        ab.StartDate < date.AddDays(1));

                    // 🚫 Already blocked
                    if (existing != null && existing.Inventory == false)
                    {
                        await transaction.RollbackAsync();
                        return Conflict(new
                        {
                            message = "Dates already blocked",
                            blockedDate = date.ToString("yyyy-MM-dd")
                        });
                    }

                    // 🟡 Available → DELETE
                    if (existing != null && existing.Inventory == true)
                    {
                        _context.AvailabilityBlocks.Remove(existing);
                    }

                    // 🟢 Insert BLOCKED
                    _context.AvailabilityBlocks.Add(new AvailabilityBlock
                    {
                        ListingId = request.ListingId,
                        StartDate = date,
                        EndDate = date.AddDays(1),
                        Inventory = false,
                        Status = "Blocked",
                        BlockType = "GuestBooking",
                        Source = "GuestPortal",
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "Dates blocked successfully",
                    blockedDates = dates.Select(d => d.ToString("yyyy-MM-dd"))
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, ex.Message);
            }
        }


        [HttpPatch("update-inventory")]
        public async Task<IActionResult> UpdateInventory(
            [FromQuery] int listingId,
            [FromQuery] DateTime date,
            [FromQuery] bool inventory)
        {
            if (listingId <= 0)
            {
                return BadRequest(new { message = "Listing ID must be greater than 0" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Invalid request. Please provide listingId, date, and inventory parameters." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Check if listing exists
                var listingExists = await _context.Listings
                    .AsNoTracking()
                    .AnyAsync(l => l.Id == listingId);

                if (!listingExists)
                {
                    return NotFound(new { message = "Listing not found" });
                }

                // Remove any existing availability blocks for this listing that overlap with the target date
                var existingBlocks = await _context.AvailabilityBlocks
                    .Where(ab => ab.ListingId == listingId &&
                               ab.StartDate.Date <= date.Date &&
                               ab.EndDate.Date >= date.Date)
                    .ToListAsync();

                if (existingBlocks.Any())
                {
                    _context.AvailabilityBlocks.RemoveRange(existingBlocks);
                    await _context.SaveChangesAsync();
                }

                // Create a new availability block
                var newBlock = new AvailabilityBlock
                {
                    ListingId = listingId,
                    StartDate = date.Date,
                    EndDate = date.Date,
                    BlockType = "Inventory",
                    Source = "Admin",
                    Status = inventory ? "Open" : "Blocked",
                    Inventory = inventory,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                _context.AvailabilityBlocks.Add(newBlock);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    listingId,
                    date = date.Date.ToString("yyyy-MM-dd"),
                    inventory = inventory,
                    message = "Inventory updated successfully"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new
                {
                    message = "An error occurred while updating inventory",
                    error = ex.Message
                });
            }
        }
    }
}
