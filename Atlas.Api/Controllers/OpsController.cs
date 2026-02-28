using Atlas.Api.Constants;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Services.WhatsApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Controllers
{
    /// <summary>Operational diagnostics: outbox messages, database info.</summary>
    [ApiController]
    [Route("ops")]
    [Produces("application/json")]
    [AllowAnonymous]
    public class OpsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IWebHostEnvironment _environment;
        private readonly IWhatsAppService? _whatsAppService;

        public OpsController(AppDbContext dbContext, IWebHostEnvironment environment, IWhatsAppService? whatsAppService = null)
        {
            _dbContext = dbContext;
            _environment = environment;
            _whatsAppService = whatsAppService;
        }

        /// <summary>Returns paginated outbox messages for ops diagnostics. Tenant-scoped.</summary>
        [HttpGet("outbox")]
        [ProducesResponseType(typeof(IEnumerable<OutboxMessageDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<OutboxMessageDto>>> GetOutbox(
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromQuery] bool? published,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            const int maxPageSize = 200;
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > maxPageSize) pageSize = maxPageSize;

            var query = _dbContext.OutboxMessages.AsNoTracking().AsQueryable();
            if (fromUtc.HasValue)
                query = query.Where(o => o.CreatedAtUtc >= fromUtc.Value);
            if (toUtc.HasValue)
                query = query.Where(o => o.CreatedAtUtc <= toUtc.Value);
            if (published.HasValue)
                query = query.Where(o => (published.Value ? o.Status == OutboxStatuses.Published : o.Status != OutboxStatuses.Published));

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(o => o.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OutboxMessageDto
                {
                    Id = o.Id,
                    TenantId = o.TenantId,
                    Topic = o.Topic,
                    EventType = o.EventType,
                    EntityId = o.EntityId,
                    CorrelationId = o.CorrelationId,
                    Status = o.Status,
                    CreatedAtUtc = o.CreatedAtUtc,
                    PublishedAtUtc = o.PublishedAtUtc,
                    AttemptCount = o.AttemptCount,
                    LastError = o.LastError
                })
                .ToListAsync(cancellationToken);

            Response.Headers.Append("X-Total-Count", totalCount.ToString());
            Response.Headers.Append("X-Page", page.ToString());
            Response.Headers.Append("X-Page-Size", pageSize.ToString());
            return Ok(items);
        }

        /// <summary>Returns environment metadata (server, database, marker) without exposing secrets.</summary>
        [HttpGet("db-info")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDatabaseInfo(CancellationToken cancellationToken)
        {
            var connection = _dbContext.Database.GetDbConnection();
            var marker = await _dbContext.EnvironmentMarkers
                .AsNoTracking()
                .Select(em => em.Marker)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(marker))
            {
                return Problem("Environment marker is not configured.");
            }

            return Ok(new
            {
                environment = _environment.EnvironmentName,
                server = connection.DataSource,
                database = connection.Database,
                marker
            });
        }

        /// <summary>Test WhatsApp send (Development only). GET /ops/whatsapp-test?phone=6301534168</summary>
        [HttpGet("whatsapp-test")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> TestWhatsApp([FromQuery] string phone, CancellationToken cancellationToken = default)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (_whatsAppService == null || string.IsNullOrWhiteSpace(phone))
                return BadRequest(new { error = "WhatsApp service not configured or phone missing." });

            var (success, msgId, error) = await _whatsAppService.SendBookingConfirmationAsync(
                phone, 99999, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(2), cancellationToken);

            return Ok(new
            {
                success,
                providerMessageId = msgId,
                error,
                phone,
                message = success ? "WhatsApp hello_world sent." : "WhatsApp send failed. Check error."
            });
        }
    }
}
