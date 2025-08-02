using System;

namespace Atlas.Api.Models.Reports
{
    public class MonthlyEarningsSummary
    {
        public required string Month { get; set; } = string.Empty;
        public decimal TotalFees { get; set; }
        public decimal TotalNet { get; set; }

        // Deprecated: use TotalNet + TotalFees instead
        [Obsolete("Use TotalNet + TotalFees. This property will be removed in a future release.")]
        public decimal TotalGross => TotalNet + TotalFees;
    }
}
