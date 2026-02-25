using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models
{
    public class PromoCode : ITenantOwnedEntity
    {
        public int Id { get; set; }

        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string DiscountType { get; set; } = "Percent"; // Percent or Flat

        public decimal DiscountValue { get; set; }

        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }

        public int? UsageLimit { get; set; }
        public int TimesUsed { get; set; }

        public int? ListingId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
