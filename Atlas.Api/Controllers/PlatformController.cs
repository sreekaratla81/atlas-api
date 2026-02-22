using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Filters;
using Atlas.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Controllers;

/// <summary>Platform-admin endpoints for managing all tenants. Requires "platform-admin" role.</summary>
[ApiController]
[Route("platform")]
[Authorize(Roles = "platform-admin")]
[BillingExempt]
[Produces("application/json")]
public class PlatformController : ControllerBase
{
    private readonly AppDbContext _db;

    public PlatformController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("tenants")]
    [ProducesResponseType(typeof(IEnumerable<PlatformTenantDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTenants(CancellationToken cancellationToken)
    {
        var tenants = await _db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new PlatformTenantDto
            {
                Id = t.Id,
                Name = t.Name,
                Slug = t.Slug,
                IsActive = t.IsActive,
                OwnerName = t.OwnerName,
                OwnerEmail = t.OwnerEmail,
                Plan = t.Plan,
                CreatedAtUtc = t.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        return Ok(tenants);
    }

    [HttpPost("tenants")]
    [ProducesResponseType(typeof(PlatformTenantDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTenant([FromBody] PlatformCreateTenantDto dto, CancellationToken cancellationToken)
    {
        var slugLower = dto.Slug.Trim().ToLowerInvariant();

        if (await _db.Tenants.AnyAsync(t => t.Slug == slugLower, cancellationToken))
        {
            return Conflict(new { error = $"Slug '{slugLower}' is already taken." });
        }

        var tenant = new Tenant
        {
            Name = dto.Name.Trim(),
            Slug = slugLower,
            IsActive = true,
            OwnerName = dto.OwnerName?.Trim() ?? "",
            OwnerEmail = dto.OwnerEmail?.Trim().ToLowerInvariant() ?? "",
            OwnerPhone = dto.OwnerPhone?.Trim() ?? "",
            Plan = dto.Plan ?? "free",
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(ListTenants), null, MapToDto(tenant));
    }

    [HttpPatch("tenants/{id}")]
    [ProducesResponseType(typeof(PlatformTenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchTenant(int id, [FromBody] PlatformPatchTenantDto dto, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { id }, cancellationToken);
        if (tenant is null) return NotFound();

        if (dto.IsActive.HasValue) tenant.IsActive = dto.IsActive.Value;
        if (dto.Plan != null) tenant.Plan = dto.Plan;
        if (dto.OwnerName != null) tenant.OwnerName = dto.OwnerName;
        if (dto.OwnerEmail != null) tenant.OwnerEmail = dto.OwnerEmail;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(MapToDto(tenant));
    }

    private static PlatformTenantDto MapToDto(Tenant t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Slug = t.Slug,
        IsActive = t.IsActive,
        OwnerName = t.OwnerName,
        OwnerEmail = t.OwnerEmail,
        Plan = t.Plan,
        CreatedAtUtc = t.CreatedAtUtc,
    };
}
