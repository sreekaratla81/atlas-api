using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Atlas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("availability")]
    [Produces("application/json")]
    [Authorize]
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
        /// <summary>
        /// Get listing availability for a date range
        /// </summary>
        /// <param name="listingId">Required: Listing ID</param>
        /// <param name="startDate">Required: Start date for date range (defaults to 30 days)</param>
        /// <param name="months">Optional: Number of months from start date (default: 2)</param>
        [HttpGet("listing-availability")]
        public async Task<ActionResult<ListingAvailabilityResponseDto>> GetListingAvailability(
            [FromQuery] int listingId,
            [FromQuery] DateTime? startDate,
            [FromQuery] int months = 2)
        {
            if (listingId <= 0)
            {
                ModelState.AddModelError(nameof(listingId), "Listing ID must be greater than 0.");
                return BadRequest(ModelState);
            }

            try
            {
                // Validate startDate
                if (!startDate.HasValue)
                {
                    ModelState.AddModelError(nameof(startDate), "Start date is required.");
                    return BadRequest(ModelState);
                }

                // Validate months
                if (months < 1 || months > 12)
                {
                    ModelState.AddModelError(nameof(months), "Months must be between 1 and 12.");
                    return BadRequest(ModelState);
                }

                // Calculate date range based on startDate and months
                DateTime calculatedStartDate = startDate.Value.Date;
                DateTime calculatedEndDate = calculatedStartDate.AddMonths(months).Date;

                // Fetch listing details (single query, no tracking for performance)
                var listing = await _context.Listings
                    .AsNoTracking()
                    .Where(l => l.Id == listingId)
                    .Select(l => new { l.Id, l.Name })
                    .FirstOrDefaultAsync();

                if (listing == null)
                {
                    return NotFound();
                }

                // Fetch all availability blocks for the date range in a single query
                // Using date range overlap logic: block overlaps if (block.StartDate < endDate && block.EndDate > startDate)
                var blocks = await _context.AvailabilityBlocks
                    .AsNoTracking()
                    .Where(ab => ab.ListingId == listingId
                                && ab.StartDate < calculatedEndDate
                                && ab.EndDate > calculatedStartDate)
                    .Select(ab => new
                    {
                        ab.StartDate,
                        ab.EndDate,
                        ab.Status,
                        ab.Inventory,
                        ab.BlockType,
                        ab.CreatedAtUtc
                    })
                    .ToListAsync();

                // Build a dictionary for efficient date lookup
                // Key: date, Value: (status, inventory)
                var dateStatusMap = new Dictionary<DateTime, (string Status, int Inventory)>();

                // Process each day in the range
                var currentDate = calculatedStartDate;
                while (currentDate < calculatedEndDate)
                {
                    // Default to Available
                    string status = "Available";
                    int inventory = 1;

                    // Find blocks that cover this date
                    foreach (var block in blocks)
                    {
                        // Check if this date falls within the block's range
                        if (currentDate >= block.StartDate.Date && currentDate < block.EndDate.Date)
                        {
                            // Check if it's a temporary hold that hasn't expired (5 minutes)
                            bool isHold = block.BlockType.Equals("Hold", StringComparison.OrdinalIgnoreCase) 
                                       || block.Status.Equals("Hold", StringComparison.OrdinalIgnoreCase);
                            
                            if (isHold)
                            {
                                // Check if hold has expired (5 minutes from creation)
                                var holdExpiryTime = block.CreatedAtUtc.AddMinutes(5);
                                if (DateTime.UtcNow <= holdExpiryTime)
                                {
                                    // Hold is still active - takes precedence over Available but not Blocked
                                    if (status == "Available")
                                    {
                                        status = "Hold";
                                        inventory = 0;
                                    }
                                }
                                // If hold expired, treat as Available (don't override)
                            }
                            else if (!block.Inventory || block.Status.Equals("Blocked", StringComparison.OrdinalIgnoreCase))
                            {
                                // Permanent block - highest priority, overrides everything
                                status = "Blocked";
                                inventory = 0;
                                break; // Blocked is final, no need to check other blocks
                            }
                        }
                    }

                    dateStatusMap[currentDate] = (status, inventory);
                    currentDate = currentDate.AddDays(1);
                }

                // Build response with availability array
                var availabilityList = dateStatusMap
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp => new DateAvailabilityDto
                    {
                        Date = kvp.Key.ToString("yyyy-MM-dd"),
                        Status = kvp.Value.Status,
                        Inventory = kvp.Value.Inventory
                    })
                    .ToList();

                var response = new ListingAvailabilityResponseDto
                {
                    ListingId = listing.Id,
                    ListingName = listing.Name,
                    Availability = availabilityList
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving listing availability for listing {ListingId}", listingId);
                return StatusCode(500, "Internal server error");
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

                // 🔹 Fetch ALL existing rows that overlap with requested date range in ONE query
                // Two date ranges overlap if: (existing.StartDate < request.EndDate && existing.EndDate > request.StartDate)
                var existingRows = await _context.AvailabilityBlocks
                    .Where(ab =>
                        ab.ListingId == request.ListingId &&
                        ab.StartDate < request.EndDate.Date &&
                        ab.EndDate > request.StartDate.Date)
                    .ToListAsync();

                // Check for overlapping blocked periods before processing dates
                var conflictingBlock = existingRows
                    .FirstOrDefault(ab => ab.Inventory == false);

                if (conflictingBlock != null)
                {
                    await transaction.RollbackAsync();
                    
                    // Format the conflicting date range for user-friendly display
                    var conflictStart = conflictingBlock.StartDate.Date.ToString("yyyy-MM-dd");
                    var conflictEnd = conflictingBlock.EndDate.Date.AddDays(-1).ToString("yyyy-MM-dd");
                    var conflictRange = conflictStart == conflictEnd 
                        ? conflictStart 
                        : $"{conflictStart}–{conflictEnd}";
                    
                    return StatusCode(422, new
                    {
                        message = $"Selected dates overlap with an existing blocked period ({conflictRange}). Please choose a different date range.",
                        conflictingDateRange = new
                        {
                            startDate = conflictStart,
                            endDate = conflictEnd
                        }
                    });
                }

                foreach (var date in dates)
                {
                    var existing = existingRows.FirstOrDefault(ab =>
                        ab.StartDate >= date &&
                        ab.StartDate < date.AddDays(1));

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
                // Using exclusive end date pattern: StartDate <= date < EndDate
                var existingBlocks = await _context.AvailabilityBlocks
                    .Where(ab => ab.ListingId == listingId &&
                               ab.StartDate.Date <= date.Date &&
                               ab.EndDate.Date > date.Date)
                    .ToListAsync();

                if (existingBlocks.Any())
                {
                    _context.AvailabilityBlocks.RemoveRange(existingBlocks);
                    await _context.SaveChangesAsync();
                }

                // Create a new availability block.
                // AvailabilityService excludes listings when block.Status == "Active".
                // Use "Active" when blocking (inventory=false) so guest availability excludes the listing.
                var newBlock = new AvailabilityBlock
                {
                    ListingId = listingId,
                    StartDate = date.Date,
                    EndDate = date.Date.AddDays(1),  // Exclusive end date (next day)
                    BlockType = "Inventory",
                    Source = "Admin",
                    Status = inventory ? "Open" : "Active",
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
