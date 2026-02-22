
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;

namespace Atlas.Api.Controllers
{
    /// <summary>Incident reporting and management.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class IncidentsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public IncidentsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<IncidentResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<IncidentResponseDto>>> GetAll()
        {
            var items = await _context.Incidents.ToListAsync();
            return Ok(items.Select(MapToResponseDto));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(IncidentResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IncidentResponseDto>> Get(int id)
        {
            var item = await _context.Incidents.FindAsync(id);
            if (item == null) return NotFound();
            return MapToResponseDto(item);
        }

        [HttpPost]
        [ProducesResponseType(typeof(IncidentResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<IncidentResponseDto>> Create(IncidentCreateDto dto)
        {
            var item = new Incident
            {
                ListingId = dto.ListingId,
                BookingId = dto.BookingId,
                Description = dto.Description,
                ActionTaken = dto.ActionTaken,
                Status = dto.Status,
                CreatedBy = dto.CreatedBy,
                CreatedOn = DateTime.UtcNow
            };
            _context.Incidents.Add(item);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = item.Id }, MapToResponseDto(item));
        }

        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, IncidentCreateDto dto)
        {
            var existing = await _context.Incidents.FindAsync(id);
            if (existing == null) return NotFound();

            existing.ListingId = dto.ListingId;
            existing.BookingId = dto.BookingId;
            existing.Description = dto.Description;
            existing.ActionTaken = dto.ActionTaken;
            existing.Status = dto.Status;
            existing.CreatedBy = dto.CreatedBy;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.Incidents.FindAsync(id);
            if (item == null) return NotFound();
            _context.Incidents.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static IncidentResponseDto MapToResponseDto(Incident i) => new()
        {
            Id = i.Id,
            ListingId = i.ListingId,
            BookingId = i.BookingId,
            Description = i.Description,
            ActionTaken = i.ActionTaken,
            Status = i.Status,
            CreatedBy = i.CreatedBy,
            CreatedOn = i.CreatedOn
        };
    }
}
