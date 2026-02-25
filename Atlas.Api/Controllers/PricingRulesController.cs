using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.Models;

namespace Atlas.Api.Controllers;

[ApiController]
[Produces("application/json")]
[Authorize]
public class PricingRulesController : ControllerBase
{
    private readonly AppDbContext _db;

    public PricingRulesController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>List all pricing rules for a listing.</summary>
    [HttpGet("/api/listings/{listingId:int}/pricing-rules")]
    [ProducesResponseType(typeof(IEnumerable<PricingRuleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByListing(int listingId, CancellationToken ct)
    {
        var rules = await _db.ListingPricingRules
            .AsNoTracking()
            .Where(r => r.ListingId == listingId)
            .OrderBy(r => r.RuleType).ThenByDescending(r => r.Priority).ThenBy(r => r.MinNights)
            .Select(r => MapToDto(r))
            .ToListAsync(ct);

        return Ok(rules);
    }

    /// <summary>Create a pricing rule for a listing.</summary>
    [HttpPost("/api/listings/{listingId:int}/pricing-rules")]
    [ProducesResponseType(typeof(PricingRuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(int listingId, [FromBody] PricingRuleCreateDto dto, CancellationToken ct)
    {
        var error = ValidateRule(dto.RuleType, dto.MinNights, dto.MaxNights, dto.DiscountPercent, dto.SeasonStart, dto.SeasonEnd);
        if (error is not null)
            return BadRequest(new { error });

        var listing = await _db.Listings.FindAsync(new object[] { listingId }, ct);
        if (listing is null)
            return NotFound(new { error = $"Listing {listingId} not found." });

        var rule = new ListingPricingRule
        {
            ListingId = listingId,
            RuleType = dto.RuleType,
            MinNights = dto.MinNights,
            MaxNights = dto.MaxNights,
            DiscountPercent = dto.DiscountPercent,
            SeasonStart = dto.SeasonStart,
            SeasonEnd = dto.SeasonEnd,
            Label = dto.Label,
            IsActive = dto.IsActive ?? true,
            Priority = dto.Priority ?? 0
        };

        _db.ListingPricingRules.Add(rule);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetByListing), new { listingId = rule.ListingId }, MapToDto(rule));
    }

    /// <summary>Update a pricing rule.</summary>
    [HttpPut("/api/listing-pricing-rules/{id:int}")]
    [ProducesResponseType(typeof(PricingRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] PricingRuleUpdateDto dto, CancellationToken ct)
    {
        var rule = await _db.ListingPricingRules.FindAsync(new object[] { id }, ct);
        if (rule is null)
            return NotFound(new { error = "Pricing rule not found." });

        var ruleType = dto.RuleType ?? rule.RuleType;
        var minNights = dto.MinNights ?? rule.MinNights;
        var maxNights = dto.MaxNights ?? rule.MaxNights;
        var discount = dto.DiscountPercent ?? rule.DiscountPercent;
        var seasonStart = dto.SeasonStart ?? rule.SeasonStart;
        var seasonEnd = dto.SeasonEnd ?? rule.SeasonEnd;

        var error = ValidateRule(ruleType, minNights, maxNights, discount, seasonStart, seasonEnd);
        if (error is not null)
            return BadRequest(new { error });

        rule.RuleType = ruleType;
        rule.MinNights = minNights;
        rule.MaxNights = maxNights;
        rule.DiscountPercent = discount;
        rule.SeasonStart = seasonStart;
        rule.SeasonEnd = seasonEnd;
        if (dto.Label is not null) rule.Label = dto.Label;
        if (dto.IsActive.HasValue) rule.IsActive = dto.IsActive.Value;
        if (dto.Priority.HasValue) rule.Priority = dto.Priority.Value;

        await _db.SaveChangesAsync(ct);

        return Ok(MapToDto(rule));
    }

    /// <summary>Delete a pricing rule.</summary>
    [HttpDelete("/api/listing-pricing-rules/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var rule = await _db.ListingPricingRules.FindAsync(new object[] { id }, ct);
        if (rule is null)
            return NotFound(new { error = "Pricing rule not found." });

        _db.ListingPricingRules.Remove(rule);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    private static string? ValidateRule(string ruleType, int? minNights, int? maxNights, decimal discount, DateTime? seasonStart, DateTime? seasonEnd)
    {
        if (ruleType is not ("LOS" or "Seasonal"))
            return "RuleType must be 'LOS' or 'Seasonal'.";

        if (ruleType == "LOS" && (minNights is null || minNights < 1))
            return "MinNights is required and must be >= 1 for LOS rules.";

        if (ruleType == "LOS" && maxNights.HasValue && maxNights < minNights)
            return "MaxNights must be >= MinNights.";

        if (ruleType == "Seasonal" && (seasonStart is null || seasonEnd is null))
            return "SeasonStart and SeasonEnd are required for Seasonal rules.";

        if (ruleType == "Seasonal" && seasonStart.HasValue && seasonEnd.HasValue && seasonEnd < seasonStart)
            return "SeasonEnd must be on or after SeasonStart.";

        if (discount <= 0 || discount > 100)
            return "DiscountPercent must be between 0 (exclusive) and 100.";

        return null;
    }

    private static PricingRuleDto MapToDto(ListingPricingRule r) => new()
    {
        Id = r.Id,
        ListingId = r.ListingId,
        RuleType = r.RuleType,
        MinNights = r.MinNights,
        MaxNights = r.MaxNights,
        DiscountPercent = r.DiscountPercent,
        SeasonStart = r.SeasonStart,
        SeasonEnd = r.SeasonEnd,
        Label = r.Label,
        IsActive = r.IsActive,
        Priority = r.Priority,
        CreatedAt = r.CreatedAt
    };
}

public class PricingRuleCreateDto
{
    public string RuleType { get; set; } = "LOS";
    public int? MinNights { get; set; }
    public int? MaxNights { get; set; }
    public decimal DiscountPercent { get; set; }
    public DateTime? SeasonStart { get; set; }
    public DateTime? SeasonEnd { get; set; }
    public string? Label { get; set; }
    public bool? IsActive { get; set; }
    public int? Priority { get; set; }
}

public class PricingRuleUpdateDto
{
    public string? RuleType { get; set; }
    public int? MinNights { get; set; }
    public int? MaxNights { get; set; }
    public decimal? DiscountPercent { get; set; }
    public DateTime? SeasonStart { get; set; }
    public DateTime? SeasonEnd { get; set; }
    public string? Label { get; set; }
    public bool? IsActive { get; set; }
    public int? Priority { get; set; }
}

public class PricingRuleDto
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public string RuleType { get; set; } = null!;
    public int? MinNights { get; set; }
    public int? MaxNights { get; set; }
    public decimal DiscountPercent { get; set; }
    public DateTime? SeasonStart { get; set; }
    public DateTime? SeasonEnd { get; set; }
    public string? Label { get; set; }
    public bool IsActive { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
}
