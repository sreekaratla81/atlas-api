using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs;

public class CreateQuoteRequestDto
{
    public int ListingId { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Guests { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal QuotedBaseAmount { get; set; }

    [RegularExpression("^(CustomerPays|Absorb)$")]
    public string FeeMode { get; set; } = "CustomerPays";

    public DateTime ExpiresAtUtc { get; set; }
    public bool ApplyGlobalDiscount { get; set; }
}

public class QuoteIssueResponseDto
{
    public string Token { get; set; } = string.Empty;
    public PriceBreakdownDto Breakdown { get; set; } = new();
}

public class QuoteValidateResponseDto
{
    public bool IsValid { get; set; }
    public string? Error { get; set; }
    public PriceBreakdownDto? Breakdown { get; set; }
}
