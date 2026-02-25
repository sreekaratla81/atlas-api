using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Atlas.Api.Models;

public class ListingPhoto : ITenantOwnedEntity, IAuditable
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    [ValidateNever]
    public Tenant Tenant { get; set; } = null!;

    [ForeignKey(nameof(Listing))]
    public int ListingId { get; set; }
    [ValidateNever]
    public Listing? Listing { get; set; }

    [Required, MaxLength(1000)]
    public string Url { get; set; } = null!;

    [MaxLength(200)]
    public string? OriginalFileName { get; set; }

    [MaxLength(20)]
    public string? ContentType { get; set; }

    public long SizeBytes { get; set; }

    public int SortOrder { get; set; }

    [MaxLength(300)]
    public string? Caption { get; set; }

    public bool IsCover { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
