using Atlas.Api.Data;
using Atlas.Api.DTOs;
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
                return ValidationProblem(ModelState);
            }

            if (guests <= 0)
            {
                ModelState.AddModelError(nameof(guests), "Guests must be at least 1.");
                return ValidationProblem(ModelState);
            }

            if (checkOut.Date <= checkIn.Date)
            {
                ModelState.AddModelError(nameof(checkOut), "Checkout must be after check-in.");
                return ValidationProblem(ModelState);
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
    }
}
