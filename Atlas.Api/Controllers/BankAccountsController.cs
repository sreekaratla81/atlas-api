using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Controllers
{
    /// <summary>CRUD operations for bank account records.</summary>
    [ApiController]
    [Route("bankaccounts")]
    [Authorize(Roles = "platform-admin")]
    [Produces("application/json")]
    public class BankAccountsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BankAccountsController> _logger;

        public BankAccountsController(AppDbContext context, ILogger<BankAccountsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<BankAccountResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<BankAccountResponseDto>>> GetAll()
        {
            var accounts = await _context.BankAccounts.AsNoTracking().ToListAsync();
            return accounts.Select(MapToDto).ToList();
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(BankAccountResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BankAccountResponseDto>> Get(int id)
        {
            var account = await _context.BankAccounts.FirstOrDefaultAsync(x => x.Id == id);
            return account == null ? NotFound() : MapToDto(account);
        }

        [HttpPost]
        [ProducesResponseType(typeof(BankAccountResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<BankAccountResponseDto>> Create(BankAccountRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.BankName))
                return BadRequest(new { error = "BankName is required." });
            if (string.IsNullOrWhiteSpace(request.AccountNumber))
                return BadRequest(new { error = "AccountNumber is required." });

            var account = new BankAccount
            {
                BankName = request.BankName,
                AccountNumber = request.AccountNumber,
                IFSC = request.IFSC,
                AccountType = request.AccountType,
                CreatedAt = DateTime.UtcNow
            };
            _context.BankAccounts.Add(account);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = account.Id }, MapToDto(account));
        }

        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, BankAccountRequestDto request)
        {
            var account = await _context.BankAccounts.FirstOrDefaultAsync(x => x.Id == id);
            if (account == null) return NotFound();

            account.BankName = request.BankName;
            account.AccountNumber = request.AccountNumber;
            account.IFSC = request.IFSC;
            account.AccountType = request.AccountType;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            var account = await _context.BankAccounts.FirstOrDefaultAsync(x => x.Id == id);
            if (account == null) return NotFound();
            _context.BankAccounts.Remove(account);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static BankAccountResponseDto MapToDto(BankAccount account)
        {
            return new BankAccountResponseDto
            {
                Id = account.Id,
                BankName = account.BankName,
                AccountNumber = account.AccountNumber,
                IFSC = account.IFSC,
                AccountType = account.AccountType,
                CreatedAt = account.CreatedAt
            };
        }
    }
}
