using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("bankaccounts")]
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
        public async Task<ActionResult<IEnumerable<BankAccountResponseDto>>> GetAll()
        {
            var accounts = await _context.BankAccounts.AsNoTracking().ToListAsync();
            return accounts.Select(MapToDto).ToList();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BankAccountResponseDto>> Get(int id)
        {
            var account = await _context.BankAccounts.FindAsync(id);
            return account == null ? NotFound() : MapToDto(account);
        }

        [HttpPost]
        public async Task<ActionResult<BankAccountResponseDto>> Create(BankAccountRequestDto request)
        {
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
        public async Task<IActionResult> Update(int id, BankAccountRequestDto request)
        {
            var account = await _context.BankAccounts.FindAsync(id);
            if (account == null) return NotFound();

            account.BankName = request.BankName;
            account.AccountNumber = request.AccountNumber;
            account.IFSC = request.IFSC;
            account.AccountType = request.AccountType;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var account = await _context.BankAccounts.FindAsync(id);
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
