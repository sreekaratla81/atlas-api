using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Atlas.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Controllers;

[ApiController]
[Route("tenants")]
[Produces("application/json")]
public class TenantsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwtService;

    public TenantsController(AppDbContext db, IJwtTokenService jwtService)
    {
        _db = db;
        _jwtService = jwtService;
    }

    /// <summary>Self-serve tenant registration. Creates tenant + owner user, returns JWT.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TenantRegisterResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] TenantRegisterRequestDto request, CancellationToken cancellationToken)
    {
        var slugLower = request.Slug.Trim().ToLowerInvariant();

        if (await _db.Tenants.AnyAsync(t => t.Slug == slugLower, cancellationToken))
        {
            return Conflict(new { error = $"Slug '{slugLower}' is already taken." });
        }

        var tenant = new Tenant
        {
            Name = request.TenantName.Trim(),
            Slug = slugLower,
            IsActive = true,
            OwnerName = request.OwnerName.Trim(),
            OwnerEmail = request.OwnerEmail.Trim().ToLowerInvariant(),
            OwnerPhone = request.OwnerPhone.Trim(),
            Plan = "free",
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(cancellationToken);

        var user = new User
        {
            TenantId = tenant.Id,
            Name = request.OwnerName.Trim(),
            Email = request.OwnerEmail.Trim().ToLowerInvariant(),
            Phone = request.OwnerPhone.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = "Owner",
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        var token = _jwtService.GenerateToken(user, tenant);

        return CreatedAtAction(nameof(GetPublic), new { slug = tenant.Slug }, new TenantRegisterResponseDto
        {
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            Slug = tenant.Slug,
            Token = token,
        });
    }

    /// <summary>Public tenant info for guest portal branding. No auth required.</summary>
    [HttpGet("{slug}/public")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TenantPublicDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublic(string slug, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == slug.ToLowerInvariant() && t.IsActive, cancellationToken);

        if (tenant is null)
            return NotFound(new { error = "Tenant not found." });

        return Ok(new TenantPublicDto
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            LogoUrl = tenant.LogoUrl,
            BrandColor = tenant.BrandColor,
        });
    }
}
