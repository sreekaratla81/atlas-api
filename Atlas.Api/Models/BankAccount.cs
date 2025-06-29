using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models
{
    public class BankAccount
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public string BankName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string AccountNumber { get; set; } = string.Empty;

        [MaxLength(20)]
        public string IFSC { get; set; } = string.Empty;

        [MaxLength(50)]
        public string AccountType { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
