using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Atlas.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace Atlas.Api.Services.Auth;

public interface IJwtTokenService
{
    string GenerateToken(User user, Tenant? tenant);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user, Tenant? tenant)
    {
        var key = _configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key) || IsPlaceholder(key))
            return "Bearer.disabled"; // When JWT is disabled, return a sentinel so onboarding/login still return a token
        // HS256 requires at least 256 bits (32 bytes); short keys cause IDX10720 and block staff login
        if (Encoding.UTF8.GetByteCount(key) < 32)
            return "Bearer.disabled";

        var expiryHours = int.TryParse(_configuration["Jwt:ExpiryHours"], out var h) ? h : 24;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role),
        };

        if (tenant is not null)
        {
            claims.Add(new Claim("tenantId", tenant.Id.ToString()));
            claims.Add(new Claim("tenantSlug", tenant.Slug));
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var v = value.Trim();
        return string.Equals(v, "__SET_VIA_ENV_OR_AZURE__", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "__SET_VIA_ENV__", StringComparison.OrdinalIgnoreCase);
    }
}
