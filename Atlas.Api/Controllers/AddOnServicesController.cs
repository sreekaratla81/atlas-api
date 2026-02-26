using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.Models;

namespace Atlas.Api.Controllers;

[ApiController]
[Route("api/add-on-services")]
[Produces("application/json")]
[Authorize(Roles = "platform-admin")]
public class AddOnServicesController : ControllerBase
{
    private readonly AppDbContext _db;

    public AddOnServicesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AddOnServiceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var items = await _db.AddOnServices
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => MapToDto(a))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AddOnServiceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] AddOnServiceCreateDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "Name is required." });

        if (dto.Price < 0)
            return BadRequest(new { error = "Price must be >= 0." });

        var validPriceTypes = new[] { "per_booking", "per_night", "per_guest" };
        var priceType = dto.PriceType ?? "per_booking";
        if (!validPriceTypes.Contains(priceType))
            return BadRequest(new { error = "PriceType must be 'per_booking', 'per_night', or 'per_guest'." });

        var entity = new AddOnService
        {
            Name = dto.Name.Trim(),
            Description = dto.Description,
            Price = dto.Price,
            PriceType = priceType,
            Category = dto.Category,
            IsActive = dto.IsActive ?? true,
            TenantId = 0
        };

        _db.AddOnServices.Add(entity);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetAll), null, MapToDto(entity));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(AddOnServiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] AddOnServiceUpdateDto dto, CancellationToken ct)
    {
        var entity = await _db.AddOnServices.FindAsync(new object[] { id }, ct);
        if (entity is null)
            return NotFound(new { error = "Add-on service not found." });

        if (dto.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { error = "Name cannot be empty." });
            entity.Name = dto.Name.Trim();
        }

        if (dto.Description is not null)
            entity.Description = dto.Description;

        if (dto.Price.HasValue)
        {
            if (dto.Price.Value < 0)
                return BadRequest(new { error = "Price must be >= 0." });
            entity.Price = dto.Price.Value;
        }

        if (dto.PriceType is not null)
        {
            var validPriceTypes = new[] { "per_booking", "per_night", "per_guest" };
            if (!validPriceTypes.Contains(dto.PriceType))
                return BadRequest(new { error = "PriceType must be 'per_booking', 'per_night', or 'per_guest'." });
            entity.PriceType = dto.PriceType;
        }

        if (dto.Category is not null)
            entity.Category = dto.Category;

        if (dto.IsActive.HasValue)
            entity.IsActive = dto.IsActive.Value;

        await _db.SaveChangesAsync(ct);

        return Ok(MapToDto(entity));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await _db.AddOnServices.FindAsync(new object[] { id }, ct);
        if (entity is null)
            return NotFound(new { error = "Add-on service not found." });

        _db.AddOnServices.Remove(entity);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>Get active add-ons configured for a listing (public, for guest booking widget).</summary>
    [HttpGet("/api/listings/{listingId:int}/add-ons")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<ListingAddOnPublicDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetListingAddOns(int listingId, CancellationToken ct)
    {
        var addOns = await _db.ListingAddOns
            .AsNoTracking()
            .Where(la => la.ListingId == listingId && la.IsEnabled && la.AddOnService.IsActive)
            .Select(la => new ListingAddOnPublicDto
            {
                AddOnServiceId = la.AddOnServiceId,
                Name = la.AddOnService.Name,
                Description = la.AddOnService.Description,
                Price = la.OverridePrice ?? la.AddOnService.Price,
                PriceType = la.AddOnService.PriceType,
                Category = la.AddOnService.Category
            })
            .ToListAsync(ct);

        return Ok(addOns);
    }

    /// <summary>Assign an add-on service to a listing.</summary>
    [HttpPost("/api/listings/{listingId:int}/add-ons")]
    [ProducesResponseType(typeof(ListingAddOnDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignToListing(int listingId, [FromBody] ListingAddOnCreateDto dto, CancellationToken ct)
    {
        var listing = await _db.Listings.FindAsync(new object[] { listingId }, ct);
        if (listing is null)
            return NotFound(new { error = $"Listing {listingId} not found." });

        var addOn = await _db.AddOnServices.FindAsync(new object[] { dto.AddOnServiceId }, ct);
        if (addOn is null)
            return NotFound(new { error = $"Add-on service {dto.AddOnServiceId} not found." });

        var exists = await _db.ListingAddOns
            .AnyAsync(la => la.ListingId == listingId && la.AddOnServiceId == dto.AddOnServiceId, ct);

        if (exists)
            return Conflict(new { error = "This add-on is already assigned to the listing." });

        var entity = new ListingAddOn
        {
            ListingId = listingId,
            AddOnServiceId = dto.AddOnServiceId,
            IsEnabled = dto.IsEnabled ?? true,
            OverridePrice = dto.OverridePrice
        };

        _db.ListingAddOns.Add(entity);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetListingAddOns), new { listingId }, MapToListingAddOnDto(entity, addOn));
    }

    /// <summary>Remove an add-on from a listing.</summary>
    [HttpDelete("/api/listings/{listingId:int}/add-ons/{addOnId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveFromListing(int listingId, int addOnId, CancellationToken ct)
    {
        var entity = await _db.ListingAddOns
            .FirstOrDefaultAsync(la => la.ListingId == listingId && la.AddOnServiceId == addOnId, ct);

        if (entity is null)
            return NotFound(new { error = "Listing add-on assignment not found." });

        _db.ListingAddOns.Remove(entity);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    private static AddOnServiceDto MapToDto(AddOnService a) => new()
    {
        Id = a.Id,
        Name = a.Name,
        Description = a.Description,
        Price = a.Price,
        PriceType = a.PriceType,
        Category = a.Category,
        IsActive = a.IsActive,
        CreatedAt = a.CreatedAt
    };

    private static ListingAddOnDto MapToListingAddOnDto(ListingAddOn la, AddOnService a) => new()
    {
        Id = la.Id,
        ListingId = la.ListingId,
        AddOnServiceId = la.AddOnServiceId,
        AddOnName = a.Name,
        IsEnabled = la.IsEnabled,
        OverridePrice = la.OverridePrice
    };
}

public class AddOnServiceCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? PriceType { get; set; }
    public string? Category { get; set; }
    public bool? IsActive { get; set; }
}

public class AddOnServiceUpdateDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public string? PriceType { get; set; }
    public string? Category { get; set; }
    public bool? IsActive { get; set; }
}

public class AddOnServiceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string PriceType { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ListingAddOnCreateDto
{
    public int AddOnServiceId { get; set; }
    public bool? IsEnabled { get; set; }
    public decimal? OverridePrice { get; set; }
}

public class ListingAddOnDto
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public int AddOnServiceId { get; set; }
    public string AddOnName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public decimal? OverridePrice { get; set; }
}

public class ListingAddOnPublicDto
{
    public int AddOnServiceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string PriceType { get; set; } = string.Empty;
    public string? Category { get; set; }
}
