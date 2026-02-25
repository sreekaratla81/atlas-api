using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models;

public class AddOnService : ITenantOwnedEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    [Required][MaxLength(100)]
    public string Name { get; set; } = "";

    [MaxLength(500)]
    public string? Description { get; set; }

    public decimal Price { get; set; }

    [MaxLength(20)]
    public string PriceType { get; set; } = "per_booking";

    [MaxLength(50)]
    public string? Category { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
