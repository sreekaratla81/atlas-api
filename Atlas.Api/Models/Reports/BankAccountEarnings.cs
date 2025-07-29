namespace Atlas.Api.Models.Reports
{
    public class BankAccountEarnings
    {
        public required string Bank { get; set; } = string.Empty;
        public required string AccountDisplay { get; set; } = string.Empty;
        public decimal AmountReceived { get; set; }
    }
}
