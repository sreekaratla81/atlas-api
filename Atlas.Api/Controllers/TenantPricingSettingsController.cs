using Atlas.Api.DTOs;
using Atlas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Controllers;

/// <summary>Tenant-level pricing configuration.</summary>
[ApiController]
[Route("tenant/settings/pricing")]
[Produces("application/json")]
[AllowAnonymous]
public class TenantPricingSettingsController : ControllerBase
{
    private readonly ITenantPricingSettingsService _service;

    public TenantPricingSettingsController(ITenantPricingSettingsService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(TenantPricingSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantPricingSettingsDto>> Get(CancellationToken cancellationToken)
    {
        var settings = await _service.GetCurrentAsync(cancellationToken);
        return Ok(new TenantPricingSettingsDto
        {
            ConvenienceFeePercent = settings.ConvenienceFeePercent,
            GlobalDiscountPercent = settings.GlobalDiscountPercent,
            UpdatedAtUtc = settings.UpdatedAtUtc,
            UpdatedBy = settings.UpdatedBy
        });
    }

    [HttpPut]
    [ProducesResponseType(typeof(TenantPricingSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TenantPricingSettingsDto>> Put([FromBody] UpdateTenantPricingSettingsDto request, CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateCurrentAsync(request, cancellationToken);
        return Ok(new TenantPricingSettingsDto
        {
            ConvenienceFeePercent = updated.ConvenienceFeePercent,
            GlobalDiscountPercent = updated.GlobalDiscountPercent,
            UpdatedAtUtc = updated.UpdatedAtUtc,
            UpdatedBy = updated.UpdatedBy
        });
    }
}
