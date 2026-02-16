using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("ops")]
    public class OpsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IWebHostEnvironment _environment;

        public OpsController(AppDbContext dbContext, IWebHostEnvironment environment)
        {
            _dbContext = dbContext;
            _environment = environment;
        }

        [HttpGet("outbox")]
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
                query = query.Where(o => (published.Value ? o.Status == "Published" : o.Status != "Published"));

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
            return Ok(items);
        }

        [HttpGet("db-info")]
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
    }
}
