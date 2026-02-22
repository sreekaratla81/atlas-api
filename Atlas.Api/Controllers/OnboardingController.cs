using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Filters;
using Atlas.Api.Models;
using Atlas.Api.Services.Auth;
using Atlas.Api.Services.Onboarding;
using Atlas.Api.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Controllers;

/// <summary>
/// Tenant onboarding flow: start → profile → documents → publish.
/// POST /onboarding/start is AllowAnonymous (rate-limited).
/// All other endpoints require the JWT issued by start or login.
/// </summary>
[ApiController]
[Route("onboarding")]
[BillingExempt]
[Produces("application/json")]
public class OnboardingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwt;
    private readonly OnboardingService _onboarding;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Services.Billing.CreditsService _credits;

    public OnboardingController(AppDbContext db, IJwtTokenService jwt, OnboardingService onboarding, ITenantContextAccessor tenantAccessor, Services.Billing.CreditsService credits)
    {
        _db = db;
        _jwt = jwt;
        _onboarding = onboarding;
        _tenantAccessor = tenantAccessor;
        _credits = credits;
    }

    /// <summary>
    /// Start onboarding: creates Tenant + User (HostAdmin) + draft Property + draft Listing.
    /// Returns JWT + created IDs + next step.
    /// </summary>
    [HttpPost("start")]
    [AllowAnonymous]
    [EnableRateLimiting("mutations")]
    [ProducesResponseType(typeof(OnboardingStartResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start([FromBody] OnboardingStartRequestDto request, CancellationToken ct)
    {
        var emailLower = request.Email.Trim().ToLowerInvariant();
        var slug = GenerateSlug(request.DisplayName);

        if (await _db.Tenants.AnyAsync(t => t.Slug == slug, ct))
        {
            slug = $"{slug}-{DateTime.UtcNow:HHmmss}";
            if (await _db.Tenants.AnyAsync(t => t.Slug == slug, ct))
                return Conflict(new { error = "Could not generate a unique slug. Try a different name." });
        }

        if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == emailLower, ct))
            return Conflict(new { error = "An account with this email already exists. Please login instead." });

        var tenant = new Tenant
        {
            Name = request.DisplayName.Trim(),
            Slug = slug,
            IsActive = true,
            OwnerName = request.DisplayName.Trim(),
            OwnerEmail = emailLower,
            OwnerPhone = request.Phone.Trim(),
            Plan = "free",
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);

        var user = new User
        {
            TenantId = tenant.Id,
            Name = request.DisplayName.Trim(),
            Email = emailLower,
            Phone = request.Phone.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = "Owner",
        };
        _db.Users.Add(user);

        var profile = new TenantProfile
        {
            TenantId = tenant.Id,
            DisplayName = request.DisplayName.Trim(),
            City = request.City?.Trim(),
            Pincode = request.Pincode?.Trim(),
            PrimaryEmail = emailLower,
            PrimaryPhone = request.Phone.Trim(),
            OnboardingStatus = "Draft",
        };
        _db.TenantProfiles.Add(profile);

        var property = new Property
        {
            TenantId = tenant.Id,
            Name = request.DisplayName.Trim(),
            Address = request.AddressLine?.Trim() ?? request.City?.Trim() ?? "TBD",
            Type = request.PropertyType?.Trim() ?? "Homestay",
            OwnerName = request.DisplayName.Trim(),
            ContactPhone = request.Phone.Trim(),
            CommissionPercent = 0,
            Status = "Draft",
        };
        _db.Properties.Add(property);
        await _db.SaveChangesAsync(ct);

        var listing = new Listing
        {
            TenantId = tenant.Id,
            PropertyId = property.Id,
            Name = $"{request.DisplayName.Trim()} - Room 1",
            Floor = 0,
            Type = request.PropertyType?.Trim() ?? "Room",
            Status = "Draft",
            WifiName = "",
            WifiPassword = "",
            MaxGuests = request.RoomCount ?? 2,
        };
        _db.Listings.Add(listing);

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenant.Id,
            ActorUserId = user.Id,
            Action = "onboarding.started",
            EntityType = "Tenant",
            EntityId = tenant.Id.ToString(),
        });

        await _db.SaveChangesAsync(ct);
        await _onboarding.SeedChecklistAsync(tenant.Id, ct);
        await _credits.ProvisionTrialAsync(tenant.Id, ct);

        var token = _jwt.GenerateToken(user, tenant);

        return Created($"/onboarding/status", new OnboardingStartResponseDto
        {
            TenantSlug = tenant.Slug,
            TenantId = tenant.Id,
            PropertyId = property.Id,
            ListingId = listing.Id,
            UserId = user.Id,
            Token = token,
            NextStep = "profile",
        });
    }

    /// <summary>Onboarding status with checklist and publish blockers.</summary>
    [HttpGet("status")]
    [Authorize]
    [ProducesResponseType(typeof(OnboardingStatusResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var tenantId = _tenantAccessor.TenantId ?? 0;
        var profile = await _db.TenantProfiles.FindAsync(new object[] { tenantId }, ct);
        var checklist = await _db.OnboardingChecklistItems
            .Where(i => i.TenantId == tenantId)
            .OrderBy(i => i.Stage).ThenBy(i => i.Key)
            .ToListAsync(ct);

        var blockers = await _onboarding.GetPublishBlockersAsync(tenantId, ct);

        return Ok(new OnboardingStatusResponseDto
        {
            OnboardingStatus = profile?.OnboardingStatus ?? "Unknown",
            Profile = new OnboardingProfileSummaryDto
            {
                DisplayName = profile?.DisplayName,
                LegalName = profile?.LegalName,
                BusinessType = profile?.BusinessType,
                City = profile?.City,
                State = profile?.State,
                Pincode = profile?.Pincode,
                HasPan = !string.IsNullOrEmpty(profile?.PanLast4),
                Gstin = profile?.Gstin,
                PrimaryEmail = profile?.PrimaryEmail,
                PrimaryPhone = profile?.PrimaryPhone,
            },
            Checklist = checklist.Select(i => new OnboardingChecklistItemDto
            {
                Key = i.Key,
                Title = i.Title,
                Stage = i.Stage,
                Status = i.Status,
                Blocking = i.Blocking,
                DueAtUtc = i.DueAtUtc,
            }).ToList(),
            PublishBlockers = blockers,
        });
    }

    /// <summary>Update tenant profile (legal/tax details).</summary>
    [HttpPut("profile")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile([FromBody] OnboardingProfileUpdateDto dto, CancellationToken ct)
    {
        var tenantId = _tenantAccessor.TenantId ?? 0;
        var profile = await _db.TenantProfiles.FindAsync(new object[] { tenantId }, ct);
        if (profile is null) return NotFound(new { error = "Tenant profile not found." });

        if (dto.LegalName is not null) profile.LegalName = dto.LegalName;
        if (dto.DisplayName is not null) profile.DisplayName = dto.DisplayName;
        if (dto.BusinessType is not null) profile.BusinessType = dto.BusinessType;
        if (dto.RegisteredAddressLine is not null) profile.RegisteredAddressLine = dto.RegisteredAddressLine;
        if (dto.City is not null) profile.City = dto.City;
        if (dto.State is not null) profile.State = dto.State;
        if (dto.Pincode is not null) profile.Pincode = dto.Pincode;
        if (dto.Gstin is not null) profile.Gstin = dto.Gstin;
        if (dto.PlaceOfSupplyState is not null) profile.PlaceOfSupplyState = dto.PlaceOfSupplyState;
        if (dto.PrimaryEmail is not null) profile.PrimaryEmail = dto.PrimaryEmail;
        if (dto.PrimaryPhone is not null) profile.PrimaryPhone = dto.PrimaryPhone;

        if (!string.IsNullOrWhiteSpace(dto.Pan))
        {
            profile.PanLast4 = dto.Pan[^4..];
            profile.PanHash = BCrypt.Net.BCrypt.HashPassword(dto.Pan.ToUpperInvariant());

            var panItem = await _db.OnboardingChecklistItems
                .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Key == "pan_upload", ct);
            if (panItem is not null) panItem.Status = "Complete";
        }

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            Action = "onboarding.profile_updated",
            EntityType = "TenantProfile",
            EntityId = tenantId.ToString(),
        });

        await _db.SaveChangesAsync(ct);
        return Ok(new { status = "updated" });
    }

    /// <summary>Upload KYC document (multipart form).</summary>
    [HttpPost("documents")]
    [Authorize]
    [ProducesResponseType(typeof(HostKycDocumentDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> UploadDocument(
        [FromForm] string docType,
        [FromForm] IFormFile file,
        CancellationToken ct)
    {
        var tenantId = _tenantAccessor.TenantId ?? 0;

        // TODO: Upload file to blob storage (Azure Blob / S3 / Cloudflare R2).
        // For now, store a placeholder URL. Replace with actual blob upload.
        var fileUrl = $"/uploads/{tenantId}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

        var doc = new HostKycDocument
        {
            TenantId = tenantId,
            DocType = docType,
            FileUrl = fileUrl,
            OriginalFileName = file.FileName,
            Status = "Pending",
        };
        _db.HostKycDocuments.Add(doc);

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            Action = "onboarding.document_uploaded",
            EntityType = "HostKycDocument",
            EntityId = docType,
        });

        await _db.SaveChangesAsync(ct);

        return Created($"/onboarding/documents/{doc.Id}", new HostKycDocumentDto
        {
            Id = doc.Id,
            DocType = doc.DocType,
            FileUrl = doc.FileUrl,
            OriginalFileName = doc.OriginalFileName,
            Status = doc.Status,
            CreatedAtUtc = doc.CreatedAtUtc,
        });
    }

    /// <summary>Prefill property info from Airbnb URL or pasted text (public metadata only).</summary>
    [HttpPost("airbnb/prefill")]
    [Authorize]
    [ProducesResponseType(typeof(AirbnbPrefillResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AirbnbPrefill([FromBody] AirbnbPrefillRequestDto request)
    {
        var result = await _onboarding.ExtractAirbnbMetadataAsync(request.AirbnbUrl, request.PastedText);
        return Ok(result);
    }

    /// <summary>
    /// Publish: validates PublishGate checklist, sets listing + tenant active.
    /// Returns 200 on success, 422 with blockers if not ready.
    /// </summary>
    [HttpPost("publish")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Publish(CancellationToken ct)
    {
        var tenantId = _tenantAccessor.TenantId ?? 0;
        var blockers = await _onboarding.GetPublishBlockersAsync(tenantId, ct);

        if (blockers.Count > 0)
            return UnprocessableEntity(new { error = "Cannot publish. Pending requirements:", blockers });

        var profile = await _db.TenantProfiles.FindAsync(new object[] { tenantId }, ct);
        if (profile is not null) profile.OnboardingStatus = "Published";

        var listings = await _db.Listings.Where(l => l.Status == "Draft").ToListAsync(ct);
        foreach (var l in listings) l.Status = "Active";

        var properties = await _db.Properties.Where(p => p.Status == "Draft").ToListAsync(ct);
        foreach (var p in properties) p.Status = "Active";

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            Action = "onboarding.published",
            EntityType = "Tenant",
            EntityId = tenantId.ToString(),
        });

        await _db.SaveChangesAsync(ct);
        return Ok(new { status = "published", listingsActivated = listings.Count });
    }

    private static string GenerateSlug(string displayName)
    {
        var slug = displayName.Trim().ToLowerInvariant();
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9]+", "-");
        slug = slug.Trim('-');
        if (slug.Length < 3) slug = $"host-{slug}";
        if (slug.Length > 50) slug = slug[..50].TrimEnd('-');
        return slug;
    }
}
