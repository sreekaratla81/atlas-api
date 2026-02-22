
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;

namespace Atlas.Api.Controllers
{
    /// <summary>User account management.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<UserResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetAll()
        {
            var users = await _context.Users.ToListAsync();
            return Ok(users.Select(MapToDto));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserResponseDto>> Get(int id)
        {
            var item = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);
            return item == null ? NotFound() : Ok(MapToDto(item));
        }

        [HttpPost]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<UserResponseDto>> Create(UserCreateDto dto)
        {
            var user = new User
            {
                TenantId = 0,
                Name = dto.Name,
                Email = dto.Email,
                Phone = dto.Phone,
                Role = dto.Role,
                PasswordHash = dto.Password
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = user.Id }, MapToDto(user));
        }

        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, UserCreateDto dto)
        {
            var existing = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (existing == null) return NotFound();

            existing.Name = dto.Name;
            existing.Phone = dto.Phone;
            existing.Email = dto.Email;
            existing.PasswordHash = dto.Password;
            existing.Role = dto.Role;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound();
            _context.Users.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static UserResponseDto MapToDto(User user) => new()
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role
        };
    }
}
