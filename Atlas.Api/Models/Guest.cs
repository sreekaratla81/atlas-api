
namespace Atlas.Api.Models
{
    public class Guest : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
        public required string Name { get; set; }
        public required string Phone { get; set; }
        public required string Email { get; set; }
        public string? IdProofUrl { get; set; }
    }
}
