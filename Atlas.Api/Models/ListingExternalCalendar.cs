using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Atlas.Api.Models;

public class ListingExternalCalendar : ITenantOwnedEntity
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    [Required]
    [ForeignKey(nameof(Listing))]
    public int ListingId { get; set; }
    public Listing Listing { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = "";

    [Required]
    [MaxLength(2000)]
    public string ICalUrl { get; set; } = "";

    public DateTime? LastSyncAtUtc { get; set; }

    [MaxLength(500)]
    public string? LastSyncError { get; set; }

    public int SyncedEventCount { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
