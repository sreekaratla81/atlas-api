using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs;

public class PlatformTenantDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public bool IsActive { get; set; }
    public string OwnerName { get; set; } = null!;
    public string OwnerEmail { get; set; } = null!;
    public string Plan { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}

public class PlatformCreateTenantDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = null!;

    [Required, MaxLength(50), RegularExpression(@"^[a-z0-9][a-z0-9\-]{1,48}[a-z0-9]$")]
    public string Slug { get; set; } = null!;

    [MaxLength(100)]
    public string? OwnerName { get; set; }

    [EmailAddress, MaxLength(200)]
    public string? OwnerEmail { get; set; }

    [MaxLength(20)]
    public string? OwnerPhone { get; set; }

    [MaxLength(20)]
    public string? Plan { get; set; }
}

public class PlatformPatchTenantDto
{
    public bool? IsActive { get; set; }
    public string? Plan { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerEmail { get; set; }
}
