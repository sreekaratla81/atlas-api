using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Controllers;

[ApiController]
[Route("auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwtService;

    public AuthController(AppDbContext db, IJwtTokenService jwtService)
    {
        _db = db;
        _jwtService = jwtService;
    }

    /// <summary>Authenticate with email + password. Returns JWT with tenantId and role claims.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { error = "Invalid email or password." });
        }

        if (!user.Tenant.IsActive)
        {
            return Unauthorized(new { error = "Tenant account is suspended." });
        }

        var token = _jwtService.GenerateToken(user, user.Tenant);

        return Ok(new LoginResponseDto
        {
            Token = token,
            Email = user.Email,
            Name = user.Name,
            Role = user.Role,
            TenantId = user.TenantId,
            TenantSlug = user.Tenant.Slug,
            TenantName = user.Tenant.Name,
        });
    }
}
