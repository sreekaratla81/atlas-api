using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.DTOs;

namespace Atlas.Api.Controllers;

/// <summary>Communication log retrieval.</summary>
[ApiController]
[Route("api/communication-logs")]
[Produces("application/json")]
[Authorize(Roles = "platform-admin")]
public class CommunicationLogsController : ControllerBase
{
    private readonly AppDbContext _context;

    public CommunicationLogsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CommunicationLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CommunicationLogDto>>> GetAll(
        [FromQuery] int? bookingId,
        [FromQuery] int? guestId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? channel,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        const int maxPageSize = 200;
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > maxPageSize) pageSize = maxPageSize;

        var query = _context.CommunicationLogs.AsNoTracking().AsQueryable();
        if (bookingId.HasValue)
            query = query.Where(c => c.BookingId == bookingId.Value);
        if (guestId.HasValue)
            query = query.Where(c => c.GuestId == guestId.Value);
        if (fromUtc.HasValue)
            query = query.Where(c => c.CreatedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue)
            query = query.Where(c => c.CreatedAtUtc <= toUtc.Value);
        if (!string.IsNullOrWhiteSpace(channel))
            query = query.Where(c => c.Channel == channel);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status == status);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CommunicationLogDto
            {
                Id = c.Id,
                TenantId = c.TenantId,
                BookingId = c.BookingId,
                GuestId = c.GuestId,
                Channel = c.Channel,
                EventType = c.EventType,
                ToAddress = c.ToAddress,
                TemplateId = c.TemplateId,
                TemplateVersion = c.TemplateVersion,
                Status = c.Status,
                AttemptCount = c.AttemptCount,
                CreatedAtUtc = c.CreatedAtUtc,
                SentAtUtc = c.SentAtUtc,
                LastError = c.LastError
            })
            .ToListAsync();

        Response.Headers.Append("X-Total-Count", totalCount.ToString());
        Response.Headers.Append("X-Page", page.ToString());
        Response.Headers.Append("X-Page-Size", pageSize.ToString());
        return Ok(items);
    }
}
