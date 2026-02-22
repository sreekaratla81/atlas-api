using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;

namespace Atlas.Api.Controllers;

[ApiController]
[Route("api/message-templates")]
[Produces("application/json")]
public class MessageTemplatesController : ControllerBase
{
    private readonly AppDbContext _context;

    public MessageTemplatesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MessageTemplateResponseDto>>> GetAll(
        [FromQuery] string? eventType,
        [FromQuery] string? channel,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        const int maxPageSize = 200;
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > maxPageSize) pageSize = maxPageSize;

        var query = _context.MessageTemplates.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(m => m.EventType == eventType);
        if (!string.IsNullOrWhiteSpace(channel))
            query = query.Where(m => m.Channel == channel);
        if (isActive.HasValue)
            query = query.Where(m => m.IsActive == isActive.Value);

        var items = await query
            .OrderBy(m => m.EventType).ThenBy(m => m.Channel)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MessageTemplateResponseDto
            {
                Id = m.Id,
                TenantId = m.TenantId,
                TemplateKey = m.TemplateKey,
                EventType = m.EventType,
                Channel = m.Channel,
                ScopeType = m.ScopeType,
                ScopeId = m.ScopeId,
                Language = m.Language,
                TemplateVersion = m.TemplateVersion,
                IsActive = m.IsActive,
                Subject = m.Subject,
                Body = m.Body,
                CreatedAtUtc = m.CreatedAtUtc,
                UpdatedAtUtc = m.UpdatedAtUtc
            })
            .ToListAsync();
        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MessageTemplateResponseDto>> Get(int id)
    {
        var item = await _context.MessageTemplates
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new MessageTemplateResponseDto
            {
                Id = m.Id,
                TenantId = m.TenantId,
                TemplateKey = m.TemplateKey,
                EventType = m.EventType,
                Channel = m.Channel,
                ScopeType = m.ScopeType,
                ScopeId = m.ScopeId,
                Language = m.Language,
                TemplateVersion = m.TemplateVersion,
                IsActive = m.IsActive,
                Subject = m.Subject,
                Body = m.Body,
                CreatedAtUtc = m.CreatedAtUtc,
                UpdatedAtUtc = m.UpdatedAtUtc
            })
            .FirstOrDefaultAsync();
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<MessageTemplateResponseDto>> Create([FromBody] MessageTemplateCreateUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.EventType))
            return BadRequest(new { error = "EventType is required." });
        if (string.IsNullOrWhiteSpace(dto.Channel))
            return BadRequest(new { error = "Channel is required." });
        if (string.IsNullOrWhiteSpace(dto.Body))
            return BadRequest(new { error = "Body is required." });

        var entity = new MessageTemplate
        {
            TemplateKey = dto.TemplateKey,
            EventType = dto.EventType,
            Channel = dto.Channel,
            ScopeType = dto.ScopeType,
            ScopeId = dto.ScopeId,
            Language = dto.Language,
            TemplateVersion = dto.TemplateVersion,
            IsActive = dto.IsActive,
            Subject = dto.Subject,
            Body = dto.Body,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _context.MessageTemplates.Add(entity);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, MapToDto(entity));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<MessageTemplateResponseDto>> Update(int id, [FromBody] MessageTemplateCreateUpdateDto dto)
    {
        var entity = await _context.MessageTemplates.FirstOrDefaultAsync(m => m.Id == id);
        if (entity == null) return NotFound();

        entity.TemplateKey = dto.TemplateKey;
        entity.EventType = dto.EventType;
        entity.Channel = dto.Channel;
        entity.ScopeType = dto.ScopeType;
        entity.ScopeId = dto.ScopeId;
        entity.Language = dto.Language;
        entity.TemplateVersion = dto.TemplateVersion;
        entity.IsActive = dto.IsActive;
        entity.Subject = dto.Subject;
        entity.Body = dto.Body;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(MapToDto(entity));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.MessageTemplates.FirstOrDefaultAsync(m => m.Id == id);
        if (entity == null) return NotFound();
        _context.MessageTemplates.Remove(entity);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static MessageTemplateResponseDto MapToDto(MessageTemplate m)
    {
        return new MessageTemplateResponseDto
        {
            Id = m.Id,
            TenantId = m.TenantId,
            TemplateKey = m.TemplateKey,
            EventType = m.EventType,
            Channel = m.Channel,
            ScopeType = m.ScopeType,
            ScopeId = m.ScopeId,
            Language = m.Language,
            TemplateVersion = m.TemplateVersion,
            IsActive = m.IsActive,
            Subject = m.Subject,
            Body = m.Body,
            CreatedAtUtc = m.CreatedAtUtc,
            UpdatedAtUtc = m.UpdatedAtUtc
        };
    }
}
