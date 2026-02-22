namespace Atlas.Api.DTOs
{
    /// <summary>Request body for creating or updating a bank account.</summary>
    public class BankAccountRequestDto
    {
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string IFSC { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
    }
}
