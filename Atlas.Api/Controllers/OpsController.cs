using Atlas.Api.Data;
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
