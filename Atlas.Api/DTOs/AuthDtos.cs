using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs;

public class LoginRequestDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = null!;

    [Required, MinLength(6)]
    public string Password { get; set; } = null!;
}

public class LoginResponseDto
{
    public string Token { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Role { get; set; } = null!;
    public int TenantId { get; set; }
    public string TenantSlug { get; set; } = null!;
    public string TenantName { get; set; } = null!;
}

public class TenantRegisterRequestDto
{
    [Required, MaxLength(100)]
    public string TenantName { get; set; } = null!;

    [Required, MaxLength(50), RegularExpression(@"^[a-z0-9][a-z0-9\-]{1,48}[a-z0-9]$",
        ErrorMessage = "Slug must be 3-50 lowercase alphanumeric characters or hyphens, starting and ending with alphanumeric.")]
    public string Slug { get; set; } = null!;

    [Required, MaxLength(100)]
    public string OwnerName { get; set; } = null!;

    [Required, EmailAddress, MaxLength(200)]
    public string OwnerEmail { get; set; } = null!;

    [Required, MaxLength(20)]
    public string OwnerPhone { get; set; } = null!;

    [Required, MinLength(6)]
    public string Password { get; set; } = null!;
}

public class TenantRegisterResponseDto
{
    public int TenantId { get; set; }
    public string TenantName { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string Token { get; set; } = null!;
}

public class TenantPublicDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? LogoUrl { get; set; }
    public string? BrandColor { get; set; }
}
