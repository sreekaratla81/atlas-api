using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs;

/// <summary>Request body for issuing a price quote.</summary>
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

/// <summary>Response containing the issued quote token and price breakdown.</summary>
public class QuoteIssueResponseDto
{
    public string Token { get; set; } = string.Empty;
    public PriceBreakdownDto Breakdown { get; set; } = new();
}

/// <summary>Response indicating whether a quote token is still valid.</summary>
public class QuoteValidateResponseDto
{
    public bool IsValid { get; set; }
    public string? Error { get; set; }
    public PriceBreakdownDto? Breakdown { get; set; }
}
