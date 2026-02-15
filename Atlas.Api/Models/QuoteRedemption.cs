using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Atlas.Api.Models;

public class QuoteRedemption : ITenantOwnedEntity
{
    public long Id { get; set; }

    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string Nonce { get; set; } = string.Empty;

    public DateTime RedeemedAtUtc { get; set; }

    public int? BookingId { get; set; }
    public Booking? Booking { get; set; }
}
