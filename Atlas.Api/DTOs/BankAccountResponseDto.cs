namespace Atlas.Api.DTOs
{
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
