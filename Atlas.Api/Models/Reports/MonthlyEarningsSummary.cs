namespace Atlas.Api.Models.Reports
{
    public class MonthlyEarningsSummary
    {
        public string Month { get; set; } = string.Empty;
        public decimal TotalGross { get; set; }
        public decimal TotalFees { get; set; }
        public decimal TotalNet { get; set; }
    }
}
