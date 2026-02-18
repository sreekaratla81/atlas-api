using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs;

/// <summary>
/// Admin: Calendar pricing view for a date range (one listing per item, with daily rows).
/// </summary>
public class CalendarPricingViewDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<CalendarPricingListingDto> Listings { get; set; } = new();
}

public class CalendarPricingListingDto
{
    public int ListingId { get; set; }
    public string ListingName { get; set; } = string.Empty;
    public string Currency { get; set; } = "INR";
    public decimal BaseNightlyRate { get; set; }
    public decimal? WeekendNightlyRate { get; set; }
    public List<CalendarPricingDayDto> Days { get; set; } = new();
}

public class CalendarPricingDayDto
{
    public string Date { get; set; } = string.Empty; // yyyy-MM-dd
    public decimal BaseAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ConvenienceFeePercent { get; set; }
    public decimal FinalAmount { get; set; }
    public decimal GlobalDiscountPercent { get; set; }
    public int RoomsAvailable { get; set; }
}

/// <summary>
/// Response for GET pricing/daily-summary: current date and per-listing pricing for that day.
/// </summary>
public class DailyPricingSummaryDto
{
    public string Date { get; set; } = string.Empty; // yyyy-MM-dd
    public List<DailyListingPricingDto> Listings { get; set; } = new();
}

public class DailyListingPricingDto
{
    public int ListingId { get; set; }
    public string ListingName { get; set; } = string.Empty;
    public decimal BaseAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ConvenienceFeePercent { get; set; }
    public decimal FinalAmount { get; set; }
    public decimal GlobalDiscountPercent { get; set; }
}

/// <summary>
/// Admin: Update base pricing for a listing.
/// </summary>
public class UpdateBasePricingDto
{
    public int ListingId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal BaseNightlyRate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? WeekendNightlyRate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ExtraGuestRate { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "INR";
}

/// <summary>
/// Request body for POST pricing/base (ListingId, BaseNightlyRate, WeekendNightlyRate, ExtraGuestRate, Currency). TenantId and UpdatedAtUtc are set server-side.
/// </summary>
public class ListingPricingItemDto
{
    public int ListingId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal BaseNightlyRate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? WeekendNightlyRate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ExtraGuestRate { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "INR";
}


/// <summary>
/// Admin: Upsert daily rate override (create or update by listingId + date).
/// </summary>
public class UpsertDailyRateDto
{
    public int ListingId { get; set; }

    [Required]
    public DateTime Date { get; set; }

    [Range(0, double.MaxValue)]
    public decimal NightlyRate { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "INR";

    [MaxLength(20)]
    public string Source { get; set; } = "Manual";

    [MaxLength(200)]
    public string? Reason { get; set; }
}

/// <summary>
/// Admin: Upsert daily inventory (create or update by listingId + date).
/// </summary>
public class UpsertDailyInventoryDto
{
    public int ListingId { get; set; }

    [Required]
    public DateTime Date { get; set; }

    [Range(0, int.MaxValue)]
    public int RoomsAvailable { get; set; }

    [MaxLength(20)]
    public string Source { get; set; } = "Manual";

    [MaxLength(200)]
    public string? Reason { get; set; }
}
