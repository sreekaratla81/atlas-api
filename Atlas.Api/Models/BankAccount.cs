using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models
{
    public class BankAccount
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public required string BankName { get; set; } = string.Empty;

        [MaxLength(50)]
        public required string AccountNumber { get; set; } = string.Empty;

        [MaxLength(20)]
        public required string IFSC { get; set; } = string.Empty;

        [MaxLength(50)]
        public required string AccountType { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
