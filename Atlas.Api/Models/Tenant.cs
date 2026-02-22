using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models;

public class Tenant
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Slug { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [MaxLength(100)]
    public string OwnerName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string OwnerEmail { get; set; } = string.Empty;

    [MaxLength(20)]
    public string OwnerPhone { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? CustomDomain { get; set; }

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    [MaxLength(7)]
    public string? BrandColor { get; set; }

    [MaxLength(20)]
    public string Plan { get; set; } = "free";

    public DateTime CreatedAtUtc { get; set; }
}
