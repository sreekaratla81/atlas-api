
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("guests")]
    [Produces("application/json")]
    [Authorize]
    public class GuestsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public GuestsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<GuestDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<GuestDto>>> GetAll()
        {
            var items = await _context.Guests.ToListAsync();
            return Ok(items.Select(MapToDto));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(GuestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<GuestDto>> Get(int id)
        {
            var item = await _context.Guests.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound();
            return MapToDto(item);
        }

        [HttpPost]
        [ProducesResponseType(typeof(GuestDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<GuestDto>> Create(GuestCreateDto dto)
        {
            var item = new Guest
            {
                Name = dto.Name,
                Email = dto.Email,
                Phone = dto.Phone,
                IdProofUrl = string.IsNullOrWhiteSpace(dto.IdProofUrl) ? "N/A" : dto.IdProofUrl,
                TenantId = 0
            };

            _context.Guests.Add(item);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = item.Id }, MapToDto(item));
        }

        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, GuestUpdateDto dto)
        {
            var existing = await _context.Guests.FirstOrDefaultAsync(x => x.Id == id);
            if (existing == null) return NotFound();

            existing.Name = dto.Name;
            existing.Phone = dto.Phone;
            existing.Email = dto.Email;
            existing.IdProofUrl = dto.IdProofUrl;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.Guests.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound();
            _context.Guests.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static GuestDto MapToDto(Guest g) => new()
        {
            Id = g.Id,
            Name = g.Name,
            Email = g.Email,
            Phone = g.Phone,
            IdProofUrl = g.IdProofUrl
        };
    }
}
