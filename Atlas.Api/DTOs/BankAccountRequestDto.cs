namespace Atlas.Api.DTOs
{
    public class BankAccountRequestDto
    {
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string IFSC { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
    }
}
