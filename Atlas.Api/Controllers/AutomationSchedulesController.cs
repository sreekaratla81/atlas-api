using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.DTOs;

namespace Atlas.Api.Controllers;

/// <summary>Automation schedule management.</summary>
[ApiController]
[Route("api/automation-schedules")]
[Produces("application/json")]
[AllowAnonymous]
public class AutomationSchedulesController : ControllerBase
{
    private readonly AppDbContext _context;

    public AutomationSchedulesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AutomationScheduleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AutomationScheduleDto>>> GetAll(
        [FromQuery] int? bookingId,
        [FromQuery] string? status,
        [FromQuery] string? eventType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        const int maxPageSize = 200;
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > maxPageSize) pageSize = maxPageSize;

        var query = _context.AutomationSchedules.AsNoTracking().AsQueryable();
        if (bookingId.HasValue)
            query = query.Where(a => a.BookingId == bookingId.Value);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(a => a.Status == status);
        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(a => a.EventType == eventType);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(a => a.DueAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AutomationScheduleDto
            {
                Id = a.Id,
                TenantId = a.TenantId,
                BookingId = a.BookingId,
                EventType = a.EventType,
                DueAtUtc = a.DueAtUtc,
                Status = a.Status,
                PublishedAtUtc = a.PublishedAtUtc,
                CompletedAtUtc = a.CompletedAtUtc,
                AttemptCount = a.AttemptCount,
                LastError = a.LastError
            })
            .ToListAsync();

        Response.Headers.Append("X-Total-Count", totalCount.ToString());
        Response.Headers.Append("X-Page", page.ToString());
        Response.Headers.Append("X-Page-Size", pageSize.ToString());
        return Ok(items);
    }
}
