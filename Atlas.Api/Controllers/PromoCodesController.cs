using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.Models;

namespace Atlas.Api.Controllers;

[ApiController]
[Route("api/promo-codes")]
[Produces("application/json")]
[Authorize]
public class PromoCodesController : ControllerBase
{
    private readonly AppDbContext _context;

    public PromoCodesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PromoCodeResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PromoCodeResponseDto>>> GetAll()
    {
        var items = await _context.PromoCodes
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => MapToDto(p))
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    [ProducesResponseType(typeof(PromoCodeResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PromoCodeResponseDto>> Create([FromBody] PromoCodeCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest(new { error = "Code is required." });

        if (dto.DiscountValue <= 0)
            return BadRequest(new { error = "DiscountValue must be greater than zero." });

        var discountType = dto.DiscountType ?? "Percent";
        if (discountType != "Percent" && discountType != "Flat")
            return BadRequest(new { error = "DiscountType must be 'Percent' or 'Flat'." });

        if (discountType == "Percent" && dto.DiscountValue > 100)
            return BadRequest(new { error = "Percent discount cannot exceed 100." });

        var normalizedCode = dto.Code.Trim().ToUpperInvariant();

        var exists = await _context.PromoCodes
            .AnyAsync(p => p.Code == normalizedCode);

        if (exists)
            return Conflict(new { error = $"Promo code '{normalizedCode}' already exists." });

        var entity = new PromoCode
        {
            Code = normalizedCode,
            DiscountType = discountType,
            DiscountValue = dto.DiscountValue,
            ValidFrom = dto.ValidFrom,
            ValidTo = dto.ValidTo,
            UsageLimit = dto.UsageLimit,
            ListingId = dto.ListingId,
            IsActive = dto.IsActive ?? true,
            TenantId = 0
        };

        _context.PromoCodes.Add(entity);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), null, MapToDto(entity));
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.PromoCodes.FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null)
            return NotFound();

        _context.PromoCodes.Remove(entity);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("validate")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PromoCodeValidateResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PromoCodeValidateResponseDto>> Validate([FromBody] PromoCodeValidateRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest(new { error = "Code is required." });

        var normalizedCode = dto.Code.Trim().ToUpperInvariant();

        var promo = await _context.PromoCodes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == normalizedCode && p.IsActive);

        if (promo == null)
            return Ok(InvalidResult("Promo code not found or inactive."));

        var utcNow = DateTime.UtcNow;

        if (promo.ValidFrom.HasValue && utcNow < promo.ValidFrom.Value)
            return Ok(InvalidResult("Promo code is not yet valid."));

        if (promo.ValidTo.HasValue && utcNow > promo.ValidTo.Value)
            return Ok(InvalidResult("Promo code has expired."));

        if (promo.UsageLimit.HasValue && promo.TimesUsed >= promo.UsageLimit.Value)
            return Ok(InvalidResult("Promo code usage limit reached."));

        if (promo.ListingId.HasValue && promo.ListingId.Value != dto.ListingId)
            return Ok(InvalidResult("Promo code is not valid for this listing."));

        var discountAmount = promo.DiscountType == "Percent"
            ? Math.Round(dto.Subtotal * promo.DiscountValue / 100m, 2)
            : Math.Min(promo.DiscountValue, dto.Subtotal);

        var message = promo.DiscountType == "Percent"
            ? $"{promo.DiscountValue}% off applied"
            : $"₹{promo.DiscountValue} off applied";

        return Ok(new PromoCodeValidateResponseDto
        {
            Valid = true,
            DiscountType = promo.DiscountType,
            DiscountValue = promo.DiscountValue,
            DiscountAmount = discountAmount,
            Message = message
        });
    }

    private static PromoCodeValidateResponseDto InvalidResult(string message) => new()
    {
        Valid = false,
        DiscountType = null,
        DiscountValue = 0,
        DiscountAmount = 0,
        Message = message
    };

    private static PromoCodeResponseDto MapToDto(PromoCode p) => new()
    {
        Id = p.Id,
        Code = p.Code,
        DiscountType = p.DiscountType,
        DiscountValue = p.DiscountValue,
        ValidFrom = p.ValidFrom,
        ValidTo = p.ValidTo,
        UsageLimit = p.UsageLimit,
        TimesUsed = p.TimesUsed,
        ListingId = p.ListingId,
        IsActive = p.IsActive,
        CreatedAt = p.CreatedAt
    };
}

public class PromoCodeCreateDto
{
    public string Code { get; set; } = string.Empty;
    public string? DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public int? UsageLimit { get; set; }
    public int? ListingId { get; set; }
    public bool? IsActive { get; set; }
}

public class PromoCodeResponseDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public int? UsageLimit { get; set; }
    public int TimesUsed { get; set; }
    public int? ListingId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PromoCodeValidateRequestDto
{
    public string Code { get; set; } = string.Empty;
    public int ListingId { get; set; }
    public decimal Subtotal { get; set; }
}

public class PromoCodeValidateResponseDto
{
    public bool Valid { get; set; }
    public string? DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public string Message { get; set; } = string.Empty;
}
