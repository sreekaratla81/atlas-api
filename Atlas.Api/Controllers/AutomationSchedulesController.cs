using Atlas.Api.Constants;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Controllers;

/// <summary>Automation schedule management (list, create, cancel).</summary>
[ApiController]
[Route("api/automation-schedules")]
[Produces("application/json")]
[Authorize]
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
            .OrderByDescending(a => a.DueAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => MapToDto(a))
            .ToListAsync();

        Response.Headers.Append("X-Total-Count", totalCount.ToString());
        Response.Headers.Append("X-Page", page.ToString());
        Response.Headers.Append("X-Page-Size", pageSize.ToString());
        return Ok(items);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AutomationScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AutomationScheduleDto>> Get(long id)
    {
        var item = await _context.AutomationSchedules.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        return item == null ? NotFound() : Ok(MapToDto(item));
    }

    [HttpPost]
    [ProducesResponseType(typeof(AutomationScheduleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AutomationScheduleDto>> Create([FromBody] CreateAutomationScheduleDto dto)
    {
        if (dto.BookingId <= 0)
            return BadRequest(new { error = "BookingId is required." });
        if (string.IsNullOrWhiteSpace(dto.EventType))
            return BadRequest(new { error = "EventType is required." });

        var bookingExists = await _context.Bookings.AnyAsync(b => b.Id == dto.BookingId);
        if (!bookingExists)
            return BadRequest(new { error = $"Booking {dto.BookingId} not found." });

        var entity = new AutomationSchedule
        {
            BookingId = dto.BookingId,
            EventType = dto.EventType,
            DueAtUtc = dto.DueAtUtc,
            Status = ScheduleStatuses.Pending,
            AttemptCount = 0
        };

        _context.AutomationSchedules.Add(entity);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = entity.Id }, MapToDto(entity));
    }

    [HttpPost("{id}/cancel")]
    [ProducesResponseType(typeof(AutomationScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AutomationScheduleDto>> Cancel(long id)
    {
        var entity = await _context.AutomationSchedules.FirstOrDefaultAsync(a => a.Id == id);
        if (entity == null) return NotFound();

        if (entity.Status != ScheduleStatuses.Pending)
            return BadRequest(new { error = $"Only Pending schedules can be cancelled. Current status: {entity.Status}." });

        entity.Status = ScheduleStatuses.Cancelled;
        entity.CompletedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(MapToDto(entity));
    }

    private static AutomationScheduleDto MapToDto(AutomationSchedule a) => new()
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
    };
}
