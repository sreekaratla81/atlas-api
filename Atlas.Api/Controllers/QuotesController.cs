using Atlas.Api.DTOs;
using Atlas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Controllers;

/// <summary>Quote generation and validation.</summary>
[ApiController]
[Route("quotes")]
[Produces("application/json")]
[AllowAnonymous]
public class QuotesController : ControllerBase
{
    private readonly IQuoteService _quoteService;

    public QuotesController(IQuoteService quoteService)
    {
        _quoteService = quoteService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(QuoteIssueResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QuoteIssueResponseDto>> Issue([FromBody] CreateQuoteRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _quoteService.IssueAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpGet("validate")]
    [ProducesResponseType(typeof(QuoteValidateResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QuoteValidateResponseDto>> Validate([FromQuery] string token, CancellationToken cancellationToken)
    {
        var response = await _quoteService.ValidateAsync(token, cancellationToken);
        if (!response.IsValid)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }
}
