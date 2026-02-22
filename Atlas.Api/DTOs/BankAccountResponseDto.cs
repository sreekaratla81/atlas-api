namespace Atlas.Api.DTOs
{
    /// <summary>Bank account data returned by the API.</summary>
    public class BankAccountResponseDto
    {
        public int Id { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string IFSC { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
