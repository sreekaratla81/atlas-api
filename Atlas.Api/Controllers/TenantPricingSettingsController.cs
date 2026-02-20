using Atlas.Api.DTOs;
using Atlas.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Controllers;

[ApiController]
[Route("tenant/settings/pricing")]
public class TenantPricingSettingsController : ControllerBase
{
    private readonly ITenantPricingSettingsService _service;

    public TenantPricingSettingsController(ITenantPricingSettingsService service)
    {
        _service = service;
    }

    [HttpGet]
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
