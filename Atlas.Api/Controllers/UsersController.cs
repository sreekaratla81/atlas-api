
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.Models;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetAll()
        {
            return await _context.Users.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<User>> Get(int id)
        {
            var item = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);
            return item == null ? NotFound() : item;
        }

        [HttpPost]
        public async Task<ActionResult<User>> Create(User item)
        {
            if (item.TenantId != 0)
            {
                return BadRequest("TenantId is managed by the server.");
            }
            _context.Users.Add(item);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, User item)
        {
            if (id != item.Id) return BadRequest();
            var existing = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (existing == null) return NotFound();
            if (item.TenantId != 0 && item.TenantId != existing.TenantId)
            {
                return NotFound();
            }

            existing.Name = item.Name;
            existing.Phone = item.Phone;
            existing.Email = item.Email;
            existing.PasswordHash = item.PasswordHash;
            existing.Role = item.Role;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound();
            _context.Users.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
