using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Atlas.Api.Services.Notifications;

namespace Atlas.Api.Controllers;

/// <summary>Notification message template management.</summary>
[ApiController]
[Route("api/message-templates")]
[Produces("application/json")]
[Authorize]
public class MessageTemplatesController : ControllerBase
{
    private readonly AppDbContext _context;

    public MessageTemplatesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MessageTemplateResponseDto>), StatusCodes.Status200OK)]
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

        var totalCount = await query.CountAsync();

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

        Response.Headers.Append("X-Total-Count", totalCount.ToString());
        Response.Headers.Append("X-Page", page.ToString());
        Response.Headers.Append("X-Page-Size", pageSize.ToString());
        return Ok(items);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(MessageTemplateResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
    [ProducesResponseType(typeof(MessageTemplateResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
    [ProducesResponseType(typeof(MessageTemplateResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.MessageTemplates.FirstOrDefaultAsync(m => m.Id == id);
        if (entity == null) return NotFound();
        _context.MessageTemplates.Remove(entity);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Send a test notification using a template (fills placeholders with sample data).</summary>
    [HttpPost("{id:int}/test-send")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestSend(int id, [FromBody] TestSendDto dto, CancellationToken ct)
    {
        var template = await _context.MessageTemplates.FindAsync(new object[] { id }, ct);
        if (template is null) return NotFound(new { error = "Template not found." });

        var sampleData = new Dictionary<string, string>
        {
            ["guest_name"] = dto.RecipientName ?? "Test Guest",
            ["listing_name"] = "Test Listing",
            ["checkin_date"] = DateTime.Today.AddDays(7).ToString("dd MMM yyyy"),
            ["checkout_date"] = DateTime.Today.AddDays(9).ToString("dd MMM yyyy"),
            ["total_amount"] = "\u20b95,000",
            ["booking_id"] = "TEST-001",
            ["property_name"] = "Test Property"
        };

        var body = template.Body;
        foreach (var kv in sampleData)
            body = body.Replace($"{{{{{kv.Key}}}}}", kv.Value);

        var subject = template.Subject;
        if (!string.IsNullOrEmpty(subject))
            foreach (var kv in sampleData)
                subject = subject.Replace($"{{{{{kv.Key}}}}}", kv.Value);

        var provider = HttpContext.RequestServices.GetRequiredService<INotificationProvider>();
        var channel = template.Channel?.ToLowerInvariant() ?? "email";
        var to = dto.RecipientAddress;
        if (string.IsNullOrWhiteSpace(to))
            return BadRequest(new { error = "RecipientAddress is required." });

        SendResult result = channel switch
        {
            "sms" => await provider.SendSmsAsync(to, body, null, ct),
            "whatsapp" => await provider.SendWhatsAppAsync(to, body, null, ct),
            _ => await provider.SendEmailAsync(to, subject, body, ct)
        };

        return Ok(new { sent = result.Success, channel, to, error = result.Error, providerMessageId = result.ProviderMessageId });
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

public class TestSendDto
{
    public string RecipientAddress { get; set; } = "";
    public string? RecipientName { get; set; }
}
