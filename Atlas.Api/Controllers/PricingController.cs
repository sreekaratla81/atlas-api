using Atlas.Api.DTOs;
using Atlas.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Controllers;

[ApiController]
[Route("pricing")]
public class PricingController : ControllerBase
{
    private readonly PricingService _pricingService;

    public PricingController(PricingService pricingService)
    {
        _pricingService = pricingService;
    }

    [HttpGet("breakdown")]
    public async Task<ActionResult<PriceBreakdownDto>> GetBreakdown([FromQuery] int listingId, [FromQuery] DateTime checkIn, [FromQuery] DateTime checkOut)
    {
        var response = await _pricingService.GetPublicBreakdownAsync(listingId, checkIn, checkOut);
        return Ok(response);
    }
}
